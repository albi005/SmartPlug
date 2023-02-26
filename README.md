# SmartPlug

C# console app to control a D-Link DSP-W115 smart plug

Based on https://github.com/jonassjoh/dspW245/

## Setup

1. Reset the plug
2. Start pairing using the app
3. Once the plug is connected your WiFi network, stop the pairing process

## Usage

```csharp
// The IP can be found in your router's admin interface
// The PIN can be found on the bottom of the plug, under "PIN Code"
using W115 w115 = await W115.Connect("192.168.0.69", 694200);

await w115.Set(SmartPlugSetting.Light, false);
await w115.Set(SmartPlugSetting.State, true);
bool state = await w115.Get(SmartPlugSetting.State);
bool light = await w115.Get(SmartPlugSetting.Light);
```

### Running the example
1. Create a file called `settings.json` in the `SmartPlug` folder
2. Add the following content to the file and replace `<IP>` and `<PIN>` with the IP and PIN of your plug:
    ```json
    {
      "ip": "<IP>",
      "pin": "<PIN>"
    }
    ```
3. Run the example using `dotnet run`