using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using STS2IncomingDamageOverlayMod.Presentation;

namespace STS2IncomingDamageOverlayMod;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "STS2IncomingDamageOverlayMod";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();

        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.Root.CallDeferred("add_child", new IncomingDamageHud());
            Logger.Info("Incoming Damage Overlay loaded.");
        }
    }
}
