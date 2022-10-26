// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Threading;
using System;
using WindowsInput;
using System.Collections.Immutable;
using WindowsInput.Native;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    internal struct InputKey
    {
        public readonly ImmutableArray<VirtualKeyCode> Modifiers;
        public readonly VirtualKeyCode VirtualKeyCode;
        public readonly char? Character;
        public readonly string? Text;

        public InputKey(VirtualKeyCode virtualKeyCode)
        {
            Modifiers = ImmutableArray<VirtualKeyCode>.Empty;
            VirtualKeyCode = virtualKeyCode;
            Character = null;
            Text = null;
        }

        public InputKey(VirtualKeyCode virtualKeyCode, ImmutableArray<VirtualKeyCode> modifiers)
        {
            Modifiers = modifiers;
            VirtualKeyCode = virtualKeyCode;
            Character = null;
            Text = null;
        }

        public InputKey(char character)
        {
            Modifiers = ImmutableArray<VirtualKeyCode>.Empty;
            VirtualKeyCode = 0;
            Character = character;
            Text = null;
        }

        public InputKey(string text)
        {
            Modifiers = ImmutableArray<VirtualKeyCode>.Empty;
            VirtualKeyCode = 0;
            Character = null;
            Text = text;
        }

        public static implicit operator InputKey(VirtualKeyCode virtualKeyCode)
            => new(virtualKeyCode);

        public static implicit operator InputKey(char character)
            => new(character);

        public static implicit operator InputKey(string text)
            => new(text);

        public void Apply(IInputSimulator simulator)
        {
            if (Character is { } c)
            {
                if (c == '\n')
                    simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                else if (c == '\t')
                    simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                else
                    simulator.Keyboard.TextEntry(c);

                return;
            }
            else if (Text is not null)
            {
                var offset = 0;
                while (offset < Text.Length)
                {
                    if (Text[offset] == '\r' && offset < Text.Length - 1 && Text[offset + 1] == '\n')
                    {
                        // Treat \r\n as a single RETURN character
                        offset++;
                        continue;
                    }
                    else if (Text[offset] == '\n')
                    {
                        simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                        offset++;
                        continue;
                    }
                    else if (Text[offset] == '\t')
                    {
                        simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                        offset++;
                        continue;
                    }
                    else
                    {
                        var nextSpecial = Text.IndexOfAny(new[] { '\r', '\n', '\t' }, offset);
                        var endOfCurrentSegment = nextSpecial < 0 ? Text.Length : nextSpecial;
                        simulator.Keyboard.TextEntry(Text[offset..endOfCurrentSegment]);
                        offset = endOfCurrentSegment;
                    }
                }

                return;
            }

            if (Modifiers.IsEmpty)
            {
                simulator.Keyboard.KeyPress(VirtualKeyCode);
            }
            else
            {
                simulator.Keyboard.ModifiedKeyStroke(Modifiers, VirtualKeyCode);
            }
        }
    }

    [TestService]
    internal partial class InputInProcess
    {
        internal Task SendAsync(InputKey key, CancellationToken cancellationToken)
            => SendAsync(new InputKey[] { key }, cancellationToken);

        internal Task SendAsync(InputKey[] keys, CancellationToken cancellationToken)
        {
            return SendAsync(
                simulator =>
                {
                    foreach (var key in keys)
                    {
                        key.Apply(simulator);
                    }
                }, cancellationToken);
        }

        internal async Task SendAsync(Action<IInputSimulator> callback, CancellationToken cancellationToken)
        {
            // AbstractSendKeys runs synchronously, so switch to a background thread before the call
            await TaskScheduler.Default;

            TestServices.JoinableTaskFactory.Run(async () =>
            {
                await TestServices.Editor.ActivateAsync(cancellationToken);
            });

            callback(new InputSimulator());

            TestServices.JoinableTaskFactory.Run(async () =>
            {
                await WaitForApplicationIdleAsync(cancellationToken);
            });
        }

        internal void Send(string keys)
        {
            SendKeys.Send(keys);
        }
    }
}

