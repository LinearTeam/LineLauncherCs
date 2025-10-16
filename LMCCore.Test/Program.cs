// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using LMC;
using LMC.LifeCycle;
using LMCCore.Account.OAuth;
using LMCCore.Java;
using LMCCore.Utils;

await Startup.Initialize();
bool done = false;
var acc = await MicrosoftOAuth.StartOAuth(rpt =>
{
    Console.WriteLine(rpt.Step + " / " + rpt.TotalStep + " : " + rpt.Message);
    if(rpt.Step == rpt.TotalStep) done = true;
});
while (true)
{
    if (done)
    {
        Console.WriteLine(JsonSerializer.Serialize(acc, JsonUtils.DefaultSerializeOptions));
    }
    await Task.Delay(200);
}