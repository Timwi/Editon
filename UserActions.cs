using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

namespace Editon
{
    partial class EditonProgram
    {
        static void exit() { _exit = true; }

        static void moveCursorUp() { moveCursor(_cursorX, _cursorY > 0 ? _cursorY - 1 : _cursorY); }
        static void moveCursorRight() { moveCursor(_cursorX + 1, _cursorY); }
        static void moveCursorDown() { moveCursor(_cursorX, _cursorY + 1); }
        static void moveCursorLeft() { moveCursor(_cursorX > 0 ? _cursorX - 1 : _cursorX, _cursorY); }
        static void moveCursorUpFar()
        {
            moveCursor(
                _cursorX,
                _file.Items
                    .Where(item => item.Y < _cursorY && item.StoppableAtX(_cursorX))
                    .MaxOrDefault(item => item.Y, 0));
        }
        static void moveCursorRightFar()
        {
            moveCursor(
                _file.Items
                    .Where(item => item.X > _cursorX && item.StoppableAtY(_cursorY))
                    .MinOrDefault(item => item.X, (int?) null)
                    ?? _file.Items
                        .Where(item => item.ContainsY(_cursorY))
                        .MaxOrDefault(item => item.X2, 0),
                _cursorY);
        }
        static void moveCursorDownFar()
        {
            moveCursor(
                _cursorX,
                _file.Items
                    .Where(item => item.Y > _cursorY && item.StoppableAtX(_cursorX))
                    .MinOrDefault(item => item.Y, (int?) null)
                    ?? _file.Items
                        .Where(item => item.ContainsX(_cursorX))
                        .MaxOrDefault(item => item.Y2, 0));
        }
        static void moveCursorLeftFar()
        {
            moveCursor(
                _file.Items
                    .Where(item => item.X < _cursorX && item.StoppableAtY(_cursorY))
                    .MaxOrDefault(item => item.X, 0),
                _cursorY);
        }
        static void moveCursorHome()
        {
            moveCursor(
                _cursorX == 0 ? _file.Items.Where(item => item.StoppableAtY(_cursorY)).MinOrDefault(item => item.X, 0) : 0,
                _cursorY
            );
        }
        static void moveCursorEnd()
        {
            moveCursor(
                _file.Items.Where(item => item.StoppableAtY(_cursorY)).MaxOrDefault(item => item.X2, 0),
                _cursorY);
        }
        static void moveCursorHomeFar()
        {
            moveCursor(0, 0);
        }
        static void moveCursorEndFar()
        {
            moveCursor(0, _file.Items.MaxOrDefault(i => i.Y2, 0));
        }
        static void moveCursorPageUp()
        {
            _vertScroll.Value = Math.Max(0, _vertScroll.Value - EditorHeight);
            moveCursor(_cursorX, Math.Max(0, _cursorY - EditorHeight));
            invalidateAll();
        }
        static void moveCursorPageDown()
        {
            _vertScroll.Value += EditorHeight;
            moveCursor(_cursorX, _cursorY + EditorHeight);
            invalidateAll();
        }

        static void enterMoveMode()
        {
            if (_selectedItem == null)
            {
                DlgMessage.Show("No item is selected.", "Error", DlgType.Error);
                return;
            }

            _mode = EditMode.Moving;
            Invalidate(_selectedItem);
        }
        static void leaveMoveMode()
        {
            _mode = EditMode.Cursor;
            Invalidate(_selectedItem);
            moveCursor(_selectedItem.X, _selectedItem.Y);
        }

        static void moveUp() { move(Direction.Up); }
        static void moveRight() { move(Direction.Right); }
        static void moveDown() { move(Direction.Down); }
        static void moveLeft() { move(Direction.Left); }
    }
}
