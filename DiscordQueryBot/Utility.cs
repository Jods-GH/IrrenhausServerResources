using A2S.Structs;
using A2S;
using Discord;
using SteamStorefrontAPI.Classes;
using SteamStorefrontAPI;
using System.Net;
using System.Net.NetworkInformation;
/// <summary>
/// The utility class for getting info
/// </summary>
public class Utility
{
    private static readonly object _lock = new object();

    public static async Task<SteamApp> getGameInfo(int GameId)
    {
        SteamApp steamApp = await AppDetails.GetAsync(GameId);
        return steamApp;
    }


    public static async Task PingChecker(IUserMessage message,ServerEmbed serverEmbed, CancellationTokenSource ct)
    {
        Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} setting up embed updater for: " + serverEmbed.ServerDomain + ":" + serverEmbed.ServerPort);
        bool moretodo = true;
        while (moretodo)
        {
            try
            {
                ct.Token.ThrowIfCancellationRequested();
                ServerInfo? serverInfo = GetServerInfo(serverEmbed.ServerDomain, serverEmbed.ServerPort);
                Embed embed;
                if (serverInfo.HasValue)
                {
                    if(serverInfo.Value.Players > 0)
                    {
                        serverEmbed.LastActivity = DateTime.Now;
                    }
                    serverEmbed.LastOnline = DateTime.Now;
                    embed = DiscordEmbedCreator.CreateEmbedOnline(serverInfo.Value, Color.Green, serverEmbed).Result;
                }
                else
                {
                    embed = DiscordEmbedCreator.CreateEmbedOffline(serverEmbed).Result;
                }

                ComponentBuilder builder = new();
                ButtonBuilder button = new();
                button.WithLabel("join");
                button.WithStyle(ButtonStyle.Link);
                button.WithUrl("https://Irrenhaus.tech");
                builder.WithButton(button);
                message.ModifyAsync(msg => {msg.Embed = embed; msg.Components = builder.Build(); });                  
                Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Updated Server Info for {serverEmbed.ServerDomain}:{serverEmbed.ServerPort}");
                Thread.Sleep(60000);
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Canceling task for {serverEmbed.ServerDomain}:{serverEmbed.ServerPort}");
                moretodo = false;
            }
        }
    }


   public static ServerInfo? GetServerInfo(string address, int port)
    {
        try
        {
            lock (_lock)
            {
                IPAddress ip = Dns.GetHostEntry(address).AddressList.First(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                ServerInfo result = Server.Query(ip.ToString(), port, 10);
                return result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[INFO] {DateTime.Now.ToString("HH:mm:ss")} Server detected as offline: {address}:{port}");
            return null;
        }
    }

    public static long? PingHost(string nameOrAddress)
    {
        Ping pinger = null;
        long? ping = null;
        try
        {
            pinger = new Ping();
            PingReply reply = pinger.Send(nameOrAddress);
            ping = reply.RoundtripTime;
        }
        catch (PingException)
        {
            // Discard PingExceptions and return false;
        }
        finally
        {
            if (pinger != null)
            {
                pinger.Dispose();
            }
        }

        return ping;
    }
}
