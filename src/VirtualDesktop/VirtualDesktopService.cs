using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiVirtDesk.VirtualDesktop
{
    public class DesktopInfo
    {
        public Guid Id { get; set; }
        public int Index { get; set; }
        public string Name { get; set; }
        public bool IsVisible { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2})", Index + 1, Name, Id);
        }
    }

    public class VirtualDesktopService : IVirtualDesktopNotification, IDisposable
    {
        private IServiceProvider10 _shell;
        private IVirtualDesktopManagerInternal _manager;
        private IVirtualDesktopNotificationService _notifService;
        private uint _cookie;
        private bool _isDisposed;

        public event Action<Guid, string, Guid, string> DesktopChanged;
        public event Action<Guid, string> DesktopNameChanged;
        public event Action<Guid, string> DesktopCreated;
        public event Action<Guid, Guid> DesktopDestroyed;

        public VirtualDesktopService()
        {
            Initialize();
        }

        public void Initialize()
        {
            try
            {
                Type shellType = Type.GetTypeFromCLSID(DesktopGuids.CLSID_ImmersiveShell);
                _shell = (IServiceProvider10)Activator.CreateInstance(shellType);

                Guid clsidManager = DesktopGuids.CLSID_VirtualDesktopManagerInternal;
                Guid iidManager = typeof(IVirtualDesktopManagerInternal).GUID;
                _manager = (IVirtualDesktopManagerInternal)_shell.QueryService(ref clsidManager, ref iidManager);

                Guid clsidNotif = DesktopGuids.CLSID_IVirtualNotificationService;
                Guid iidNotif = typeof(IVirtualDesktopNotificationService).GUID;
                _notifService = (IVirtualDesktopNotificationService)_shell.QueryService(ref clsidNotif, ref iidNotif);

                int hr = _notifService.Register(this, out _cookie);
                if (hr != 0)
                {
                    Console.WriteLine("[MultiVirtDesk] Warning: Registration for notifications returned 0x{0:X8}", hr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] Error initializing VirtualDesktopService: " + ex.Message);
                throw;
            }
        }

        public DesktopInfo GetCurrentDesktop()
        {
            try
            {
                IVirtualDesktop cur = _manager.GetCurrentDesktop();
                if (cur == null) return null;

                Guid id = cur.GetId();
                int idx = GetDesktopIndex(id);
                string name = ResolveDesktopName(cur, idx);

                return new DesktopInfo
                {
                    Id = id,
                    Index = idx,
                    Name = name,
                    IsVisible = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] GetCurrentDesktop error: " + ex.Message);
                return null;
            }
        }

        public List<DesktopInfo> GetDesktops()
        {
            List<DesktopInfo> result = new List<DesktopInfo>();
            try
            {
                IObjectArray array;
                _manager.GetDesktops(out array);
                if (array == null) return result;

                int count;
                array.GetCount(out count);

                Guid curId = Guid.Empty;
                try
                {
                    IVirtualDesktop cur = _manager.GetCurrentDesktop();
                    if (cur != null) curId = cur.GetId();
                }
                catch { }

                Guid iidDesktop = typeof(IVirtualDesktop).GUID;
                for (int i = 0; i < count; i++)
                {
                    object obj;
                    array.GetAt(i, ref iidDesktop, out obj);
                    IVirtualDesktop desk = obj as IVirtualDesktop;
                    if (desk != null)
                    {
                        Guid id = desk.GetId();
                        result.Add(new DesktopInfo
                        {
                            Id = id,
                            Index = i,
                            Name = ResolveDesktopName(desk, i),
                            IsVisible = (id == curId)
                        });
                        Marshal.ReleaseComObject(obj);
                    }
                }
                Marshal.ReleaseComObject(array);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] GetDesktops error: " + ex.Message);
            }
            return result;
        }

        public int GetDesktopIndex(Guid desktopId)
        {
            try
            {
                IObjectArray array;
                _manager.GetDesktops(out array);
                if (array == null) return -1;

                int count;
                array.GetCount(out count);
                Guid iidDesktop = typeof(IVirtualDesktop).GUID;

                int foundIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    object obj;
                    array.GetAt(i, ref iidDesktop, out obj);
                    IVirtualDesktop desk = obj as IVirtualDesktop;
                    if (desk != null)
                    {
                        if (desk.GetId() == desktopId)
                        {
                            foundIndex = i;
                            Marshal.ReleaseComObject(obj);
                            break;
                        }
                        Marshal.ReleaseComObject(obj);
                    }
                }
                Marshal.ReleaseComObject(array);
                return foundIndex;
            }
            catch
            {
                return -1;
            }
        }

        public bool SwitchToDesktop(Guid desktopId)
        {
            try
            {
                IVirtualDesktop desk = _manager.FindDesktop(ref desktopId);
                if (desk != null)
                {
                    _manager.SwitchDesktop(desk);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] SwitchToDesktop error: " + ex.Message);
            }
            return false;
        }

        public bool SwitchToDesktopByIndex(int index)
        {
            try
            {
                IObjectArray array;
                _manager.GetDesktops(out array);
                if (array == null) return false;

                Guid iidDesktop = typeof(IVirtualDesktop).GUID;
                object obj;
                array.GetAt(index, ref iidDesktop, out obj);
                IVirtualDesktop desk = obj as IVirtualDesktop;
                if (desk != null)
                {
                    _manager.SwitchDesktop(desk);
                    Marshal.ReleaseComObject(obj);
                    Marshal.ReleaseComObject(array);
                    return true;
                }
                Marshal.ReleaseComObject(array);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] SwitchToDesktopByIndex error: " + ex.Message);
            }
            return false;
        }

        public string ResolveDesktopName(IVirtualDesktop desktop, int fallbackIndex)
        {
            if (desktop == null) return "Desktop " + (fallbackIndex + 1);
            try
            {
                string name = desktop.GetName();
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return "Desktop " + (fallbackIndex + 1);
        }

        #region IVirtualDesktopNotification Implementation

        public void VirtualDesktopCreated(IVirtualDesktop pDesktop)
        {
            if (pDesktop == null) return;
            try
            {
                Guid id = pDesktop.GetId();
                int idx = GetDesktopIndex(id);
                string name = ResolveDesktopName(pDesktop, idx);
                if (DesktopCreated != null) DesktopCreated(id, name);
            }
            catch { }
        }

        public void VirtualDesktopDestroyBegin(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback) { }
        public void VirtualDesktopDestroyFailed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback) { }

        public void VirtualDesktopDestroyed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
        {
            try
            {
                Guid destroyedId = pDesktopDestroyed != null ? pDesktopDestroyed.GetId() : Guid.Empty;
                Guid fallbackId = pDesktopFallback != null ? pDesktopFallback.GetId() : Guid.Empty;
                if (DesktopDestroyed != null) DesktopDestroyed(destroyedId, fallbackId);
            }
            catch { }
        }

        public void VirtualDesktopMoved(IVirtualDesktop pDesktop, long nOldIndex, long nNewIndex) { }

        public void VirtualDesktopNameChanged(IVirtualDesktop pDesktop, string chName)
        {
            if (pDesktop == null) return;
            try
            {
                Guid id = pDesktop.GetId();
                if (DesktopNameChanged != null) DesktopNameChanged(id, chName);
            }
            catch { }
        }

        public void ViewVirtualDesktopChanged(object pView) { }

        public void CurrentVirtualDesktopChanged(IVirtualDesktop pDesktopOld, IVirtualDesktop pDesktopNew)
        {
            try
            {
                Guid oldId = pDesktopOld != null ? pDesktopOld.GetId() : Guid.Empty;
                Guid newId = pDesktopNew != null ? pDesktopNew.GetId() : Guid.Empty;

                int oldIdx = oldId != Guid.Empty ? GetDesktopIndex(oldId) : -1;
                int newIdx = newId != Guid.Empty ? GetDesktopIndex(newId) : -1;

                string oldName = ResolveDesktopName(pDesktopOld, oldIdx);
                string newName = ResolveDesktopName(pDesktopNew, newIdx);

                if (DesktopChanged != null)
                {
                    DesktopChanged(oldId, oldName, newId, newName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiVirtDesk] Callback error: " + ex.Message);
            }
        }

        public void VirtualDesktopWallpaperChanged(IVirtualDesktop pDesktop, string chPath) { }
        public void VirtualDesktopSwitched(IVirtualDesktop pDesktop) { }
        public void RemoteVirtualDesktopConnected(IVirtualDesktop pDesktop) { }

        #endregion

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_notifService != null && _cookie != 0)
                {
                    try
                    {
                        _notifService.Unregister(_cookie);
                    }
                    catch { }
                    _cookie = 0;
                }
            }
        }
    }
}
