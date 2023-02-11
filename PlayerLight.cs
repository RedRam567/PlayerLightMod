using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.Config;
using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace PlayerLight;

// magic syntax to add "freestanding" functions
using static FreeStandingFunctions;

public static class FreeStandingFunctions {
    // cs is a good language
    public static float maxVector3(Vector3 vector3) {
        return Math.Max(vector3.X, Math.Max(vector3.Y, vector3.Z));
    }

    /// <summary>
    /// Set highest channel to 1.0, scale rest proportionally.
    /// Do nothing if all are 0.0.
    /// </summary>
    public static Vector3 normalizeBrightness(Vector3 rgb) {
        float max = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z));
        if (max == 0f) {
            return new Vector3(0f, 0f, 0f);
        }
        // element wise divide
        return rgb / max;
    }

    /// <summary>
    /// Adjust brightness upward with something similar to gamma curve.
    /// 0 -> 0, 0.5 -> 0.75, 1 -> 1.
    /// startOffset shifts curve upward and squishes it down to the same max.
    /// </summary>
    // public static float brighten(float value, float startOffset) {
    //     return (startOffset + (1 - startOffset) * (value * (2 - value)));
    // }
}

public class PlayerLight : Mod {
    public static ConfigServer ConfigServer { get; set; }
    public static ConfigClient ConfigClient { get; set; }
}

public class PlayerExt : ModPlayer {
    public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {
        if (PlayerLight.ConfigClient.brightness < PlayerLight.ConfigServer.brightness) {
            if (PlayerLight.ConfigClient.enabled) {
                Lighting.AddLight(drawInfo.Position, PlayerLight.ConfigClient.lightColor);
            }
        } else {
            if (PlayerLight.ConfigServer.enabled) {
                Lighting.AddLight(drawInfo.Position, PlayerLight.ConfigServer.lightColor);
            }
        }

    }
}

// Configs do some ugly logic to make sure both client and server are loaded
// because client uses server color settings
// avoids doing checks/math every frame on DrawEffects

public class ConfigClient : ModConfig {
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // hide from config menu
    [JsonIgnore]
    public Vector3 lightColor;

    [JsonIgnore]
    public bool enabled;

    [Header("Brightness")]
    [Label("Brightness")]
    [Tooltip("Cannot be brighter than the server brightness.\n0-1000, Recomended range is 100-300, 1000 is torch, 200 is dim light")]
    [Range(0, 1000)]
    [DefaultValue(1000)]
    public int brightness { get; set; }

    public override void OnLoaded() {
        PlayerLight.ConfigClient = this;
        if (PlayerLight.ConfigServer is not null) {
            PlayerLight.ConfigServer.OnChanged();
        }
    }

    // ran when config settings changed or loaded for first time
    public override void OnChanged() {
        if (PlayerLight.ConfigServer == null) { return; }
        float brightness = (float)this.brightness / 1000f;

        Vector3 color = new Vector3(PlayerLight.ConfigServer.red, PlayerLight.ConfigServer.green, PlayerLight.ConfigServer.blue);
        if (PlayerLight.ConfigServer.normalize) {
            color = normalizeBrightness(color);
        }
        this.lightColor = color * brightness;
        this.enabled = maxVector3(this.lightColor) > 0;

        PlayerLight.ConfigClient = this;
    }
}

public class ConfigServer : ModConfig {
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // hide from config menu
    [JsonIgnore]
    public Vector3 lightColor;

    // avoid checking if color is 0 every frame
    [JsonIgnore]
    public bool enabled;

    [Header("Brightness")]
    [Label("Brightness")]
    [Tooltip("Clients can choose to set a lower brightness.\n0-1000, Recomended range is 100-300, 1000 is torch, 200 is dim light")]
    [Range(0, 1000)]
    [DefaultValue(200)]
    public int brightness { get; set; }

    [Label("Normalize color brightness")]
    [Tooltip("Normalize brightness for the color. Ex: (0, 0.25, 0.5) -> (0, 0.5, 1)")]
    [DefaultValue(true)]
    public bool normalize { get; set; }

    [Header("Color")]
    [Label("Red")]
    [Range(0f, 1f)]
    [DefaultValue(0.59f)]
    public float red { get; set; }

    [Label("Green")]
    [Range(0f, 1f)]
    [DefaultValue(0.52f)]
    public float green { get; set; }

    [Label("Blue")]
    [Range(0f, 1f)]
    [DefaultValue(0.7f)]
    public float blue { get; set; }

    public override void OnLoaded() {
        PlayerLight.ConfigServer = this;
        if (PlayerLight.ConfigClient is not null) {
            PlayerLight.ConfigClient.OnChanged();
        }
    }

    // ran when config settings changed or loaded for first time
    public override void OnChanged() {
        if (PlayerLight.ConfigServer is null) { return; }
        float brightness = (float)this.brightness / 1000f;

        Vector3 color = new Vector3(this.red, this.green, this.blue);
        if (this.normalize) {
            color = normalizeBrightness(color);
        }
        this.lightColor = color * brightness;
        this.enabled = maxVector3(this.lightColor) > 0;

        PlayerLight.ConfigServer = this;
    }

    // AcceptClientChanges is called on the server when a Client player attempts to change ServerSide settings in-game. By default, client changes are accepted. (As long as they don't necessitate a Reload)
    // With more effort, a mod could implement more control over changing mod settings.
    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message) {
        // Only allow server owner to change settings
        return false;
    }
}
