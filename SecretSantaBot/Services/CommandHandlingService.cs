using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private IServiceProvider _provider;
        List<string> _authorizedUsers = new List<string>() { };
        Dictionary<string, string> _addedUsers = new Dictionary<string, string>();
        Dictionary<string, SocketUser> _addedUsersChannel = new Dictionary<string, SocketUser>();
        public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;

            _discord.MessageReceived += MessageReceived;
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _provider = provider;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
            // Add additional initialization code here...
        }

        private async Task MessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            int argPos = 0;
            //if (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_discord, message);
            var result = await _commands.ExecuteAsync(context, argPos, _provider);

            if (result.Error.HasValue &&
                result.Error.Value != CommandError.UnknownCommand)
                await context.Channel.SendMessageAsync(result.ToString());


            ParseCommands(rawMessage);
        }
        public async void ParseCommands(SocketMessage message)
        {
            var user = message.Author as SocketGuildUser;
            if (message.Channel.Name != "secret-santa")
            {
                return;
            }
            string[] splits = message.Content.Split(' ');
            string command = splits[0];
            string restOfMessage = String.Join(" ", (splits.Skip(1).Take(splits.Length - 1).ToArray()));

            var user2 = message.Author as SocketGuildUser;
            SocketGuild guild = null;
            ISocketMessageChannel channel = null;
            ulong currentGuildID = 0;
            switch (command)
            {
                case "!Join":
                    if (_addedUsers.Keys.Contains(message.Author.Id.ToString()))
                    {
                        DisplayMessage(channel, "You have already been added!");
                        return;
                    }
                    _addedUsers.Add(message.Author.Id.ToString(), message.Author.Username);
                    _addedUsersChannel.Add(message.Author.Id.ToString(), message.Author);
                    File.AppendAllText("users.txt", message.Author.Id.ToString() + "|" + message.Author.Username + Environment.NewLine);
                    foreach (SocketGuildChannel s in user2.Guild.Channels)
                    {
                        if (s.Name == "secret-santa")
                        {
                            channel = (ISocketMessageChannel)s;
                            currentGuildID = user2.Guild.Id;

                            DisplayMessage(channel, user2.Username + " was added to the secret santa!");
                        }
                    }
                    break;
                case "!ShowSecretSantaUsers":
                    string message2 = "";
                    foreach (SocketGuildChannel s2 in user2.Guild.Channels)
                    {
                        if (s2.Name == "secret-santa")
                        {
                            channel = (ISocketMessageChannel)s2;
                            currentGuildID = user2.Guild.Id;
                        }
                    }
                    foreach (string s in _addedUsers.Values)
                    {
                        message2 = message2 + s + "\n";
                    }
                    DisplayMessage(channel, message2);
                    break;
                case "!SendSecretSantas":
                    //_addedUsers.Add("452213452346", "Bob");
                    //_addedUsers.Add("453513426", "Sarah");
                    //_addedUsers.Add("452223222342346", "Steve");
                    //_addedUsers.Add("123152342346", "Jim");
                    //_addedUsers.Add("1234523452356", "Joe");
                    //_addedUsers.Add("45221234213412", "Elliot");
                    //_addedUsers.Add("55242634534", "Emily");
                    //_addedUsers.Add("12312312563", "Roman");
                    if (_addedUsers.Keys.Count <= 1)
                    {
                        foreach (SocketGuildChannel s in user2.Guild.Channels)
                        {
                            if (s.Name == "secret-santa")
                            {
                                channel = (ISocketMessageChannel)s;
                                currentGuildID = user2.Guild.Id;

                                DisplayMessage(channel, "There are not enough users :(");
                                return;
                            }
                        }
                    }
                    List<string> sendList = new List<string>();
                    List<string> receiveList = new List<string>();

                    foreach (string s in _addedUsers.Keys)
                    {
                        sendList.Add(s);
                        receiveList.Add(s);
                    }

                    Random r = new Random();
                    Dictionary<string, string> pairingsNames = new Dictionary<string, string>();
                    Dictionary<string, string> pairings = new Dictionary<string, string>();
                    Dictionary<string, string> pairingsMessages = new Dictionary<string, string>();
                    while (sendList.Count > 0)
                    {
                        int sendRan = r.Next(0, sendList.Count - 1);
                        int recRan = r.Next(0, receiveList.Count - 1);

                        while (sendList[sendRan] == receiveList[recRan])
                        {
                            sendRan = r.Next(0, sendList.Count - 1);
                            recRan = r.Next(0, receiveList.Count - 1);
                        }
                        pairings.Add(sendList[sendRan], receiveList[recRan]);
                        pairingsNames.Add(_addedUsers[sendList[sendRan]], _addedUsers[receiveList[recRan]]);
                        pairingsMessages.Add(sendList[sendRan], _addedUsers[receiveList[recRan]]);
                        File.AppendAllText("pairings.txt", _addedUsers[sendList[sendRan]] + " is getting a gift for " + _addedUsers[receiveList[recRan]] + Environment.NewLine);
                        await Discord.UserExtensions.SendMessageAsync(_addedUsersChannel[sendList[sendRan]], _addedUsers[sendList[sendRan]] + " is getting a gift for " + _addedUsers[receiveList[recRan]]);
                        Thread.Sleep(500);
                        sendList.Remove(sendList[sendRan]);
                        receiveList.Remove(receiveList[recRan]);
                    }
                    DisplayMessage(channel, "The secret santas have been sent out. If you didn't receive a DM from me message the dumbass creator of this(dem).");
                    Environment.Exit(0);

                    break;
            }

        }
        public async void DisplayMessage(ISocketMessageChannel channel, string message)
        {
            await channel.SendMessageAsync(message);
        }
        public async void SendDM(IDMChannel channel, string message)
        {
            await channel.SendMessageAsync(message);
        }
        public bool CheckForAdmin(SocketMessage message)
        {
            var user = message.Author as SocketGuildUser;
            if (user.GuildPermissions.Administrator == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
