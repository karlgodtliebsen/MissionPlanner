#!/usr/bin/env bash
# Run a separate, already-built ArduCopter SITL instance and one MAVLink bridge.
# Usage: bash start-network-sitl.sh udp|ws ARDUPILOT_DIR INSTANCE SYSID LISTEN_PORT [BIND_IP] [RUN_DIR]
set -euo pipefail
mode="${1:?udp or ws}"
checkout="${2:?ArduPilot checkout}"
instance="${3:-20}"
sysid="${4:-41}"
port="${5:-14560}"
bind="${6:-127.0.0.1}"
run_dir="${7:-$(mktemp -d)}"
[[ "$mode" == udp || "$mode" == ws ]] || { echo "Mode must be udp or ws" >&2; exit 2; }
[[ "$instance" =~ ^[0-9]+$ && "$sysid" =~ ^[0-9]+$ && "$port" =~ ^[0-9]+$ ]] || exit 2
(( instance > 0 && instance < 100 && sysid > 0 && sysid < 255 && port > 0 && port < 65536 )) || exit 2
checkout="$(cd "$checkout" && pwd)"
mkdir -p "$run_dir"
cd "$run_dir"
if [[ -e STOP ]]; then
    echo "Choose a fresh run directory (STOP already exists)." >&2
    exit 2
fi
tcp_port=$((5760 + instance * 10))
pids=()
cleanup() {
    for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
    for pid in "${pids[@]}"; do wait "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT
trap 'exit 0' INT TERM
"$checkout/build/sitl/bin/arducopter" --model + --speedup 1 -I"$instance" --sysid "$sysid"     --defaults "$checkout/Tools/autotest/default_params/copter.parm" >sitl.log 2>&1 &
pids+=("$!")
sleep 2
if [[ "$mode" == udp ]]; then
    mavproxy.py --master="tcp:127.0.0.1:$tcp_port" --out="udpin:$bind:$port" --daemon >bridge.log 2>&1 &
else
    python -m websockify "$bind:$port" "127.0.0.1:$tcp_port" >bridge.log 2>&1 &
fi
pids+=("$!")
echo "SITL sysid=$sysid instance=$instance TCP=$tcp_port; $mode endpoint=$bind:$port"
echo "Logs: $PWD. Ctrl+C or create $PWD/STOP to stop only these child processes."
while [[ ! -e STOP ]]; do
    for pid in "${pids[@]}"; do
        kill -0 "$pid" 2>/dev/null || { echo "Child exited; inspect logs." >&2; exit 1; }
    done
    sleep 1
done
