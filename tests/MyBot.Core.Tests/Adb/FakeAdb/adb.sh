#!/bin/sh
# Fake adb cho test PersistentAdbShell (bản POSIX của adb.bat).
# File mode.txt cạnh file này chọn hành vi: ok (mặc định) | slow | fail.
DIR=$(dirname "$0")
echo "$*" >> "$DIR/spawns.log"

case "$*" in
    *kill-server*|*start-server*) exit 0 ;;
esac

MODE=ok
if [ -f "$DIR/mode.txt" ]; then
    MODE=$(cat "$DIR/mode.txt")
fi

while IFS= read -r line; do
    case "$MODE" in
        fail)
            echo "$line"
            case "$line" in
                "export PS1=mybot.run:") echo "mybot.run:" ;;
            esac
            ;;
        slow)
            sleep 1
            echo "$line"
            echo "SLOW-OK"
            echo "mybot.run:"
            ;;
        *)
            echo "$line"
            echo "OUTPUT-OK"
            echo "mybot.run:"
            ;;
    esac
done

echo exit >> "$DIR/exited.txt"
