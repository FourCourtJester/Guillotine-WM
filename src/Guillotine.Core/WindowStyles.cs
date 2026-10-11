namespace Guillotine.Core;

/// <summary>Win32 window style flags and the borderless transform, kept free of P/Invoke so they can be tested.</summary>
public static class WindowStyles
{
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_MAXIMIZE = 0x01000000;
    public const uint WS_CAPTION = 0x00C00000; // WS_BORDER | WS_DLGFRAME
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const uint WS_OVERLAPPEDWINDOW = WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    public const uint WS_EX_DLGMODALFRAME = 0x00000001;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_WINDOWEDGE = 0x00000100;
    public const uint WS_EX_CLIENTEDGE = 0x00000200;
    public const uint WS_EX_STATICEDGE = 0x00020000;

    private const uint FrameStyles = WS_CAPTION | WS_THICKFRAME | WS_MAXIMIZEBOX | WS_MAXIMIZE;
    private const uint FrameExStyles = WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE;

    /// <summary>True when the window has no title bar and no resize frame.</summary>
    public static bool IsBorderless(uint style) => (style & (WS_CAPTION | WS_THICKFRAME)) == 0;

    /// <summary>
    /// Removes the title bar, resize frame and edge styles and makes the window a popup, so the
    /// window rectangle and client area are the same. WS_SYSMENU and WS_MINIMIZEBOX are kept so
    /// clicking the taskbar button still minimises the game.
    /// </summary>
    public static (uint Style, uint ExStyle) MakeBorderless(uint style, uint exStyle) =>
        ((style & ~FrameStyles) | WS_POPUP, exStyle & ~FrameExStyles);

    /// <summary>
    /// Fallback used when the original styles were not saved (for example, the window was made
    /// borderless by something else): a normal resizable window with a title bar.
    /// </summary>
    public static (uint Style, uint ExStyle) MakeFramed(uint style, uint exStyle) =>
        ((style & ~WS_POPUP) | WS_OVERLAPPEDWINDOW, exStyle | WS_EX_WINDOWEDGE);
}
