
## About

LowKey is a small library that makes it easy to add global hotkeys
to .NET applications. I wrote it for [GaGa][], a minimal radio player
for the Windows tray.

[GaGa]: https://github.com/Beluki/GaGa

## Usage

Here is an example:

```csharp
using LowKey;

// get a keyboard hooker:
KeyboardHook hooker = KeyboardHook.Hooker;

// register two hotkeys:
hooker.Add("Volume up", Keys.Up, Keys.Control | Keys.Shift);
hooker.Add("Volume down", Keys.Down, Keys.Control | Keys.Shift);

// add an event handler:
hooker.HotkeyDown += (o, e) => {
    switch(e.Name)
    {
        case "Volume up":
            // what to do when ctrl+shift+up is pressed
            break;

        case "Volume down":
            // what to do when ctrl+shift+down is pressed
            break;
    }
};

// start looking for keypresses:
hooker.Hook();

// when we are done (also called when disposing)...
hooker.Unhook();
```

LowKey uses names to identify hotkeys. This makes it easier to write event
handlers, as there is no need to check what exact key and modifiers were
pressed. Also, it's possible to rebind hotkeys without event handlers breaking:

```csharp
// use alt instead of shift:
hooker.Rebind("Volume up", Keys.Up, Keys.Control | Keys.Alt);
hooker.Rebind("Volume down", Keys.Up, Keys.Control | Keys.Alt);

// the previous event handler continues working for the new key bindings
```

Modifiers are a bitwise combination of Keys.Alt, Keys.Shift and Keys.Control.
If you don't want modifiers, you can omit the parameter or use Keys.None.
The full `.Add(...)` syntax actually has four parameters:

```csharp
.Add(name, base key, modifiers = Keys.None, forward = false);
```

The last parameter indicates whether LowKey should forward keypresses
to other hooks or applications.

## Additional notes

LowKey calls event handlers asynchronously but in the thread that created
the KeyboardHook instance. It forwards keypresses immediately, without
waiting for event handlers, while still allowing to do UI operations in them
(if the thread is the UI thread).

It also means that event handler exceptions can be caught by the VS debugger.
All the exceptions that LowKey itself raises are of the type `KeyboardHookException`
including those originated in Windows API calls.

Note that for performance reasons (at least on Windows 7 and newer) there is
a limit on the maximum time a low-level hook can wait to respond. Don't block
on the event handlers for a long time, use a thread instead.

## Portability

LowKey is tested on Windows 7 and 8, using the .NET Framework 4.0+.
It has no external dependencies. Previous versions are not supported.

## Status

LowKey is feature complete, but it still needs testing. Please, report
any bugs you may find. I'll update this section once it matures enough.

## License

Like all my hobby projects, this is Free Software. See the [Documentation][]
folder for more information. No warranty though.

[Documentation]: https://github.com/Beluki/LowKey/tree/master/Documentation

