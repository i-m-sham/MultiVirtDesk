using System;
using System.Runtime.InteropServices;

namespace MultiVirtDesk.VirtualDesktop
{
    internal static class DesktopGuids
    {
        public static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        public static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        public static readonly Guid CLSID_IVirtualNotificationService = new Guid("A501FDEC-4A09-464C-AE4E-1B9C21B84918");
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    public interface IVirtualDesktop
    {
        bool IsViewVisible(object view);
        Guid GetId();
        [return: MarshalAs(UnmanagedType.HString)]
        string GetName();
        [return: MarshalAs(UnmanagedType.HString)]
        string GetWallpaperPath();
        bool IsRemote();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    public interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
    public interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void MoveViewToDesktop(object view, IVirtualDesktop desktop);
        bool CanViewMoveDesktops(object view);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);
        IVirtualDesktop CreateDesktop();
        void MoveDesktop(IVirtualDesktop desktop, int nIndex);
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
        void GetDesktopSwitchIncludeExcludeViews(IVirtualDesktop desktop, out IObjectArray unknown1, out IObjectArray unknown2);
        void SetDesktopName(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string name);
        void SetDesktopWallpaper(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string path);
        void UpdateWallpaperPathForAllDesktops([MarshalAs(UnmanagedType.HString)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE")]
    public interface IVirtualDesktopNotification
    {
        void VirtualDesktopCreated(IVirtualDesktop pDesktop);
        void VirtualDesktopDestroyBegin(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);
        void VirtualDesktopDestroyFailed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);
        void VirtualDesktopDestroyed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);
        void VirtualDesktopMoved(IVirtualDesktop pDesktop, long nOldIndex, long nNewIndex);
        void VirtualDesktopNameChanged(IVirtualDesktop pDesktop, [MarshalAs(UnmanagedType.HString)] string chName);
        void ViewVirtualDesktopChanged(object pView);
        void CurrentVirtualDesktopChanged(IVirtualDesktop pDesktopOld, IVirtualDesktop pDesktopNew);
        void VirtualDesktopWallpaperChanged(IVirtualDesktop pDesktop, [MarshalAs(UnmanagedType.HString)] string chPath);
        void VirtualDesktopSwitched(IVirtualDesktop pDesktop);
        void RemoteVirtualDesktopConnected(IVirtualDesktop pDesktop);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0CD45E71-D927-4F15-8B0A-8FEF525337BF")]
    public interface IVirtualDesktopNotificationService
    {
        [PreserveSig]
        int Register(IVirtualDesktopNotification pNotification, out uint pdwCookie);
        [PreserveSig]
        int Unregister(uint dwCookie);
    }
}
