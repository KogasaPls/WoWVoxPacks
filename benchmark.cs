using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using WoWVoxPack.AddOns.BigWigs_Voice;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        var upstream = new BigWigsVoiceUpstreamClient(client);

        var sw = Stopwatch.StartNew();
        var files = await upstream.GetSoundFilesAsync();
        sw.Stop();

        Console.WriteLine($"Found {System.Linq.Enumerable.Count(files)} files in {sw.ElapsedMilliseconds} ms");
    }
}
