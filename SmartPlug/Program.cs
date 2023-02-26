using System.Text.Json;
using SmartPlug;

Settings settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText("settings.json"))!;

using W115 w115 = await W115.Connect(settings.Ip, settings.Pin);
await w115.Set(SmartPlugSetting.Light, false);
bool state = await w115.Get(SmartPlugSetting.State);
await w115.Set(SmartPlugSetting.State, !state);
await w115.Set(SmartPlugSetting.Light, false);
