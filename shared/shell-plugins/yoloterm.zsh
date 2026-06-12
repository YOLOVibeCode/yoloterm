# YOLOTerm Shell Plugin for Zsh
# Source this file in your ~/.zshrc to enable:
#   - OSC 133 prompt marks (command history with exit codes & duration)
#   - OSC 7 current working directory reporting
#
# Installation:
#   echo 'source /path/to/yoloterm.zsh' >> ~/.zshrc
#
# Based on TermGrid's proven shell integration model.

# Bail if disabled
[[ "${YOLOTERM_PLUGIN_DISABLED:-0}" == "1" ]] && return 0

# OSC 7: Report current working directory
_yoloterm_osc7() {
  local url_encoded_cwd
  url_encoded_cwd=$(printf '%s' "$PWD" | sed 's/ /%20/g; s/!/%21/g; s/"/%22/g; s/#/%23/g; s/\$/%24/g; s/&/%26/g; s/'\''/%27/g')
  printf '\e]7;file://%s%s\a' "$HOST" "$url_encoded_cwd"
}

# OSC 133: Prompt marks for semantic zones
# Sequence A: prompt start
# Sequence B: prompt end / command start
# Sequence C: command executed (pre-exec)
# Sequence D: command finished (precmd, with exit code)

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
    # No command was executed (just pressed enter on empty line)
    printf '\e]133;D;%d\a' "$exit_code"
  fi
  
  # Report working directory
  _yoloterm_osc7
  
  # Prompt start for the next prompt
  _yoloterm_prompt_start
}

# Hook into zsh's preexec and precmd
autoload -Uz add-zsh-hook
add-zsh-hook preexec _yoloterm_preexec
add-zsh-hook precmd _yoloterm_precmd

# Emit prompt start immediately (for first prompt)
_yoloterm_prompt_start

# Note: Add _yoloterm_prompt_end to your PS1 (or PROMPT) manually:
#   PROMPT='%F{green}%n@%m%f:%F{blue}%~%f$ '$'\e]133;B\a'
# Or YOLOTerm will infer command start from first input after prompt marks.
