using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;

namespace Arale.Scripts.Cards;

/// <summary>Local description styling; Ancient cards and other characters retain their native theme.</summary>
[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class AraleCardTextStyle
{
    private static readonly string[] ColorsChanged = { "default_color", "font_outline_color", "font_shadow_color" };
    private static readonly string[] ConstantsChanged = { "outline_size", "shadow_outline_size", "shadow_offset_x", "shadow_offset_y" };
    private sealed class Original
    {
        public readonly Dictionary<string, Color?> Colors = new();
        public readonly Dictionary<string, int?> Constants = new();
        public string Text = "";
        public Original(MegaRichTextLabel label)
        {
            foreach (var key in ColorsChanged) Colors[key] = label.HasThemeColorOverride(key) ? label.GetThemeColor(key) : null;
            foreach (var key in ConstantsChanged) Constants[key] = label.HasThemeConstantOverride(key) ? label.GetThemeConstant(key) : null;
        }
        public void Restore(MegaRichTextLabel label)
        {
            foreach (var (key, value) in Colors)
                if (value.HasValue) label.AddThemeColorOverride(key, value.Value); else label.RemoveThemeColorOverride(key);
            foreach (var (key, value) in Constants)
                if (value.HasValue) label.AddThemeConstantOverride(key, value.Value); else label.RemoveThemeConstantOverride(key);
        }
    }
    private static readonly ConditionalWeakTable<NCard, Original> Originals = new();

    [HarmonyPrefix]
    private static void Prefix(NCard __instance)
    {
        if (Originals.TryGetValue(__instance, out var saved) && __instance.GetNodeOrNull<MegaRichTextLabel>("%DescriptionLabel") is { } label)
            saved.Restore(label);
    }

    [HarmonyPostfix]
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.GetNodeOrNull<MegaRichTextLabel>("%DescriptionLabel") is not { } label) return;
        // Capture the native text after generation, including upgrade / target previews.
        var saved = Originals.GetValue(__instance, _ => new Original(label));
        saved.Text = label.Text;
        Apply(__instance, label, saved, AraleSettings.DarkCardText);
    }

    internal static void RefreshVisibleCards(bool dark)
    {
        foreach (var (node, saved) in Originals)
            if (GodotObject.IsInstanceValid(node) && node.IsInsideTree() && node.GetNodeOrNull<MegaRichTextLabel>("%DescriptionLabel") is { } label)
                Apply(node, label, saved, dark);
    }

    private static void Apply(NCard node, MegaRichTextLabel label, Original saved, bool dark)
    {
        saved.Restore(label);
        bool custom = node.Model is AraleCardModel card && card.Rarity != CardRarity.Ancient
            && AraleArt.FrameFor(card.Rarity, card.Type) is not null;
        string text = saved.Text;
        if (custom && dark)
        {
            label.AddThemeColorOverride("default_color", new Color("35303F"));
            label.AddThemeColorOverride("font_outline_color", Godot.Colors.Transparent);
            label.AddThemeColorOverride("font_shadow_color", Godot.Colors.Transparent);
            foreach (var key in ConstantsChanged) label.AddThemeConstantOverride(key, 0);
            text = DarkenMarkup(text);
        }
        else if (custom)
        {
            // Preserve the mod's existing light text + outline option.
            label.AddThemeColorOverride("font_outline_color", new Color("3B293FFF"));
            label.AddThemeConstantOverride("outline_size", 4);
        }
        if (label.Text != text) label.SetTextAutoSize(text);
    }

    internal static string DarkenMarkup(string text)
    {
        // Keep semantic distinctions (improved green / reduced red), never flatten all colors to gold.
        foreach (var (tag, hex) in new[] { ("gold", "916015"), ("green", "246B35"), ("red", "A52D3F") })
            text = text.Replace("[" + tag + "]", "[color=#" + hex + "]").Replace("[/" + tag + "]", "[/color]");
        return text.Replace("#EFC851", "#916015", System.StringComparison.OrdinalIgnoreCase)
            .Replace("#7FFF00", "#246B35", System.StringComparison.OrdinalIgnoreCase)
            .Replace("#FF5555", "#A52D3F", System.StringComparison.OrdinalIgnoreCase);
    }
}
