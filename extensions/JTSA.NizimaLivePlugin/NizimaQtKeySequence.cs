using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA.NizimaLivePlugin;

internal static class NizimaQtKeySequence
{
    public static bool TryFormat(Key key, ModifierKeys modifiers, out string sequence)
    {
        sequence = "";
        if ((modifiers & ModifierKeys.Windows) != 0)
            return false;

        if (IsModifierKey(key))
            return false;

        var keyName = MapKeyName(key);
        if (keyName is null)
            return false;

        var parts = new List<string>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & ModifierKeys.Alt) != 0)
            parts.Add("Alt");
        parts.Add(keyName);
        sequence = string.Join('+', parts);
        return true;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.System;

    private static string? MapKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString();
        if (key is >= Key.D0 and <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        if (key is >= Key.F1 and <= Key.F24)
            return key.ToString();

        return key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Return => "Return",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Question",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemOpenBrackets => "BracketLeft",
            Key.OemCloseBrackets => "BracketRight",
            Key.OemBackslash => "Backslash",
            Key.OemPipe => "Bar",
            Key.OemTilde => "Asciitilde",
            Key.NumPad0 => "0",
            Key.NumPad1 => "1",
            Key.NumPad2 => "2",
            Key.NumPad3 => "3",
            Key.NumPad4 => "4",
            Key.NumPad5 => "5",
            Key.NumPad6 => "6",
            Key.NumPad7 => "7",
            Key.NumPad8 => "8",
            Key.NumPad9 => "9",
            Key.Add => "Plus",
            Key.Subtract => "Minus",
            Key.Multiply => "Asterisk",
            Key.Divide => "Slash",
            Key.Decimal => "Period",
            Key.NumLock => "NumLock",
            _ => null
        };
    }
}

internal static class NizimaHotkeyCapture
{
    public static void Attach(TextBox textBox)
    {
        textBox.IsReadOnly = true;
        textBox.Focusable = true;
        textBox.CaretBrush = System.Windows.Media.Brushes.Transparent;
        InputMethod.SetIsInputMethodEnabled(textBox, false);
        textBox.PreviewKeyDown += (_, e) =>
        {
            if (IsModifierKey(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (!NizimaQtKeySequence.TryFormat(e.Key, Keyboard.Modifiers, out var sequence))
            {
                e.Handled = true;
                return;
            }

            textBox.Text = sequence;
            e.Handled = true;
        };
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.System;
}
