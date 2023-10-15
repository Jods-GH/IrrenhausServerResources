using System;
using Discord.WebSocket;
using Discord;
using A2S;
using Discord.Commands;
using SteamStorefrontAPI;
using SteamStorefrontAPI.Classes;
using Discord.Net;
using Newtonsoft.Json;
using static JsonFileReader;
using LiteDB;
using A2S.Structs;
using System.Net;
using Aliyun.OSS;

/// <summary>
/// Summary description for Class1
/// </summary>
public class ServerQueryBot

{
    public static Task Main(string[] args) => new ServerQueryBot().MainAsync();
    private DiscordSocketClient _client;
    private DataBaseHelper DBhelper;
    private Dictionary<ulong, CancellationTokenSource> tasklist = new Dictionary<ulong, CancellationTokenSource>();
    BotSettings BotSettings;
    public async Task MainAsync()
    {
        _client = new DiscordSocketClient();
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Bot is starting");
        BotSettings = JsonFileReader.Read<BotSettings>("Settings.json");
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Read bot settings");
        await _client.LoginAsync(TokenType.Bot, BotSettings.BotToken);
        await _client.StartAsync();
        CommandService command = new CommandService();
        LoggingService logger = new LoggingService(_client, command);
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Bot started");
        _client.Ready += botReady;
        _client.SlashCommandExecuted += SlashCommandHandler;
        DBhelper = new DataBaseHelper();
        await Task.Delay(-1);
    }


    private async Task botReady()
    {
        await createCommands();
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} SettingUpServers");
        foreach (ServerEmbed embed in DBhelper.getAllEntrys())
        {
            ITextChannel channel = (ITextChannel)_client.GetChannelAsync(embed.ChannelID).Result;
            IUserMessage message = (IUserMessage)channel.GetMessageAsync(embed.MessageID).Result;
            var tokenSource = new CancellationTokenSource();
            tasklist.Add(message.Id, tokenSource);
            embed.AdditionalDescription = BotSettings.ServerDescriptions.GetValueOrDefault(embed.ServerDomain + ":" + embed.ServerPort, "");
            Task.Run(() => Utility.PingChecker(message, embed, tokenSource), tokenSource.Token);
        }


    }
    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.CommandName == "add-server")
        {
            command.RespondAsync($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Creating Server");
            SocketSlashCommandData data = command.Data;
            var domain = data.Options.First();
            var port = data.Options.Last();
            Boolean success = setupServer((SocketTextChannel)command.Channel, (string)domain.Value, (long)port.Value).IsCompletedSuccessfully;
            if (success == true)
            {
                command.DeleteOriginalResponseAsync();
            }
            else
            {
                command.ModifyOriginalResponseAsync(msg => msg.Content = $"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Server creation failed");
            }
        }
        else if(command.CommandName == "remove-server")
        {
            command.RespondAsync($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Removing Server");
            SocketSlashCommandData data = command.Data;
            var domain = data.Options.First();
            var port = data.Options.Last();
            Boolean success = removeServer((string)domain.Value, (long)port.Value);
             if (success == true)
            {
                command.DeleteOriginalResponseAsync();
            }
            else
            {
                command.ModifyOriginalResponseAsync(msg => msg.Content = $"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Server deletion failed");
            }
        }
        else
        {
            Console.WriteLine($"[WARNING] Illegal command name: {command.CommandName}");
        }
        
    }
    private bool removeServer(string domain,long port)
    {
        IPAddress ip = Dns.GetHostEntry(domain).AddressList.First(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        List<ServerEmbed> collection = DBhelper.getDatabaseEntry(ip.ToString(), (int)port);
        bool deleted = false;
        foreach (ServerEmbed embed in collection)
        {
            deleted = true;
            bool threadrunning = tasklist.TryGetValue(embed.MessageID, out CancellationTokenSource value);
            if (threadrunning == true)
            {
                value.Cancel();
            }
            DBhelper.removeFromDatabase(embed.MessageID);
            SocketTextChannel channel =(SocketTextChannel)_client.GetChannelAsync(embed.ChannelID).Result;
            channel.GetMessageAsync(embed.MessageID).Result.DeleteAsync();
            Console.WriteLine($"[INFO] deleted Server: {domain}:{port} with MessageID: {embed.MessageID}");
        }
        if (deleted == false)
        {
            Console.WriteLine($"[INFO] Server deletion unsuccessfull: {domain}:{port} collection is {collection.Count}");
        }
        return deleted;
    }

    private async Task setupServer(SocketTextChannel chat,String domain,long port)
    {
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Adding Server: {domain}:{port}");
        ServerInfo? serverInfo = Utility.GetServerInfo(domain, (int)port);
        Embed embed;
        if (serverInfo.HasValue)
        {
            embed = DiscordEmbedCreator.SetupEmbed(serverInfo.Value, Color.DarkBlue, domain,(int)port).Result;
        }
        else
        {
            throw new Exception($"[ERROR] {DateTime.Now.ToString("HH:mm:ss")} Server Creation for {domain}:{port} failed");
        }
        IUserMessage message = chat.SendMessageAsync(embed: embed).Result;
        IPAddress ip = Dns.GetHostEntry(domain).AddressList.First(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        ServerEmbed embedData = new()
        {
            MessageID = message.Id,
            ServerID = chat.Guild.Id,
            ChannelID = chat.Id,
            ServerDomain = ip.ToString(),
            ServerPort = (int)port,
            GameID = serverInfo.Value.GameId,
            Map = serverInfo.Value.Map,
            MaxPlayers = serverInfo.Value.MaxPlayers,
            Name = serverInfo.Value.Name,
            LastActivity = DateTime.Now,
            AdditionalDescription = BotSettings.ServerDescriptions.GetValueOrDefault(ip.ToString()+":"+port, "")
        };
        DBhelper.addToDatabase(embedData);
        var tokenSource = new CancellationTokenSource();
        tasklist.Add(message.Id, tokenSource);
        Task.Run(() => Utility.PingChecker(message, embedData,tokenSource), tokenSource.Token);
    }

    private async Task createCommands()
    {
        List<ApplicationCommandProperties> applicationCommandProperties = new();
        try
        {
            // Simple help slash command.
            SlashCommandBuilder addServerCommand = new SlashCommandBuilder();
            addServerCommand.WithName("add-server");
            addServerCommand.WithDescription("adds a Server");
            addServerCommand.AddOption("domain", ApplicationCommandOptionType.String, "The ip or domain of the server",true);
            addServerCommand.AddOption("port", ApplicationCommandOptionType.Integer, "The Query Port of the server", true);
            applicationCommandProperties.Add(addServerCommand.Build());


            SlashCommandBuilder removeServerCommand = new SlashCommandBuilder();
            removeServerCommand.WithName("remove-server");
            removeServerCommand.WithDescription("removes a Server");
            removeServerCommand.AddOption("domain", ApplicationCommandOptionType.String, "The ip or domain of the server", true);
            removeServerCommand.AddOption("port", ApplicationCommandOptionType.Integer, "The Query Port of the server", true);
            applicationCommandProperties.Add(removeServerCommand.Build());




            await _client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
        }
        catch (ApplicationCommandException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }


}
