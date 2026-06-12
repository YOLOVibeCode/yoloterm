# YOLOTerm Shell Plugin for Bash
# Source this file in your ~/.bashrc to enable:
#   - OSC 133 prompt marks (command history with exit codes & duration)
#   - OSC 7 current working directory reporting
#
# Installation:
#   echo 'source /path/to/yoloterm.bash' >> ~/.bashrc
#
# Based on TermGrid's proven shell integration model.

# Bail if disabled
[[ "${YOLOTERM_PLUGIN_DISABLED:-0}" == "1" ]] && return 0

# OSC 7: Report current working directory
_yoloterm_osc7() {
  local url_encoded_cwd
  url_encoded_cwd=$(printf '%s' "$PWD" | sed 's/ /%20/g; s/!/%21/g; s/"/%22/g; s/#/%23/g; s/\$/%24/g; s/&/%26/g; s/'\''/%27/g')
  printf '\e]7;file://%s%s\a' "$HOSTNAME" "$url_encoded_cwd"
}

# OSC 133: Prompt marks
_yoloterm_prompt_start() {
  printf '\e]133;A\a'
}

_yoloterm_prompt_end() {
  printf '\e]133;B\a'
}

_yoloterm_preexec() {
  # Command executed mark (with timestamp for duration calc)
  _yoloterm_cmd_start=$(date +%s%3N)  # milliseconds
  printf '\e]133;C\a'
}

_yoloterm_precmd() {
  local exit_code=$?
  
  # Command finished mark with exit code and duration
  if [[ -n "$_yoloterm_cmd_start" ]]; then
    local cmd_end=$(date +%s%3N)
    local duration=$(( cmd_end - _yoloterm_cmd_start ))
    printf '\e]133;D;%d;%d\a' "$exit_code" "$duration"
    unset _yoloterm_cmd_start
  else
    # No command was executed
    printf '\e]133;D;%d\a' "$exit_code"
  fi
  
  # Report working directory
  _yoloterm_osc7
  
  # Prompt start for the next prompt
  _yoloterm_prompt_start
}

# Bash hooks via DEBUG and PROMPT_COMMAND
# preexec equivalent: DEBUG trap (fires before each command)
trap '_yoloterm_preexec' DEBUG

# precmd equivalent: PROMPT_COMMAND (fires before each prompt)
if [[ -z "$PROMPT_COMMAND" ]]; then
  PROMPT_COMMAND="_yoloterm_precmd"
else
  PROMPT_COMMAND="_yoloterm_precmd; $PROMPT_COMMAND"
fi

# Emit prompt start immediately
_yoloterm_prompt_start

# Add prompt end mark to PS1:
#   PS1='\[\e[32m\]\u@\h\[\e[0m\]:\[\e[34m\]\w\[\e[0m\]\$ \[\e]133;B\a\]'
