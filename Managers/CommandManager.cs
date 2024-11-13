using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoVisor.Managers;

public class CommandManager
{
    private delegate IntPtr ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unk1, byte unk2);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F2 48 8B F9 45 84 C9")]
    private readonly ProcessChatBoxDelegate _processChatBox = null!;

    public CommandManager()
        => Dalamud.Interop.InitializeFromAttributes(this);

    public unsafe bool Execute(string message)
    {
        // First try to process the command through Dalamud.
        if (Dalamud.Commands.ProcessCommand(message))
        {
            Dalamud.Log.Verbose("Executed Dalamud command \"{Message:l}\".", message);
            return true;
        }

        var uiModulePtr = (nint)UIModule.Instance();
        if (uiModulePtr == nint.Zero)
        {
            Dalamud.Log.Error("Can not execute \"{Message:l}\" because no uiModulePtr is available.", message);
            return false;
        }

        // Then prepare a string to send to the game itself.
        var (text, length) = PrepareString(message);
        var payload = PrepareContainer(text, length);

        _processChatBox.Invoke(uiModulePtr, payload, IntPtr.Zero, (byte)0);

        Marshal.FreeHGlobal(payload);
        Marshal.FreeHGlobal(text);
        return false;
    }

    private static (IntPtr, long) PrepareString(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var mem   = Marshal.AllocHGlobal(bytes.Length + 30);
        Marshal.Copy(bytes, 0, mem, bytes.Length);
        Marshal.WriteByte(mem + bytes.Length, 0);
        return (mem, bytes.Length + 1);
    }

    private static IntPtr PrepareContainer(IntPtr message, long length)
    {
        var mem = Marshal.AllocHGlobal(400);
        Marshal.WriteInt64(mem,        message.ToInt64());
        Marshal.WriteInt64(mem + 0x8,  64);
        Marshal.WriteInt64(mem + 0x10, length);
        Marshal.WriteInt64(mem + 0x18, 0);
        return mem;
    }
}
