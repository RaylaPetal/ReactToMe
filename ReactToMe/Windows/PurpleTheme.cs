using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace ReactToMe.Windows;

/// <summary>Violet ImGui theme applied only around this plugin's own windows (pushed/popped just
/// around WindowSystem.Draw), so it never bleeds into other plugins' or the game's own UI.</summary>
public static class PurpleTheme
{
    private static Vector4 Rgb(byte r, byte g, byte b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

    private static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    private static readonly Vector4 Accent = Rgb(139, 92, 246);       // violet-500
    private static readonly Vector4 AccentHovered = Rgb(167, 139, 250); // violet-400
    private static readonly Vector4 AccentActive = Rgb(124, 58, 237);   // violet-600
    private static readonly Vector4 FrameBg = Rgb(42, 27, 61);
    private static readonly Vector4 FrameBgHovered = Rgb(61, 42, 92);
    private static readonly Vector4 FrameBgActive = Rgb(76, 53, 117);
    private static readonly Vector4 TitleBgActive = Rgb(76, 29, 149);
    private static readonly Vector4 CheckMark = Rgb(196, 181, 253);    // violet-300
    private static readonly Vector4 SeparatorColor = Rgb(139, 92, 246, 0.5f);

    public static ImRaii.ColorDisposable Push() => ImRaii.PushColor(ImGuiCol.Button, Accent)
        .Push(ImGuiCol.ButtonHovered, AccentHovered)
        .Push(ImGuiCol.ButtonActive, AccentActive)
        .Push(ImGuiCol.Header, WithAlpha(Accent, 0.55f))
        .Push(ImGuiCol.HeaderHovered, WithAlpha(AccentHovered, 0.7f))
        .Push(ImGuiCol.HeaderActive, AccentActive)
        .Push(ImGuiCol.FrameBg, FrameBg)
        .Push(ImGuiCol.FrameBgHovered, FrameBgHovered)
        .Push(ImGuiCol.FrameBgActive, FrameBgActive)
        .Push(ImGuiCol.CheckMark, CheckMark)
        .Push(ImGuiCol.SliderGrab, Accent)
        .Push(ImGuiCol.SliderGrabActive, AccentActive)
        .Push(ImGuiCol.TitleBgActive, TitleBgActive)
        .Push(ImGuiCol.Tab, WithAlpha(Accent, 0.4f))
        .Push(ImGuiCol.TabHovered, AccentHovered)
        .Push(ImGuiCol.TabActive, AccentActive)
        .Push(ImGuiCol.Separator, SeparatorColor)
        .Push(ImGuiCol.SeparatorHovered, AccentHovered)
        .Push(ImGuiCol.ResizeGrip, WithAlpha(Accent, 0.3f))
        .Push(ImGuiCol.ResizeGripHovered, AccentHovered);
}
