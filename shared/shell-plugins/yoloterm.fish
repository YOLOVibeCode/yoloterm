# YOLOTerm Shell Plugin for Fish
# Source this file in your ~/.config/fish/config.fish to enable:
#   - OSC 133 prompt marks (command history with exit codes & duration)
#   - OSC 7 current working directory reporting
#
# Installation:
#   echo 'source /path/to/yoloterm.fish' >> ~/.config/fish/config.fish
#
# Based on TermGrid's proven shell integration model.

# Bail if disabled
if set -q YOLOTERM_PLUGIN_DISABLED; and test "$YOLOTERM_PLUGIN_DISABLED" = "1"
    exit 0
end

# OSC 7: Report current working directory
function _yoloterm_osc7
    set -l url_encoded_cwd (string replace -a ' ' '%20' -- $PWD)
    set url_encoded_cwd (string replace -a '!' '%21' -- $url_encoded_cwd)
    set url_encoded_cwd (string replace -a '"' '%22' -- $url_encoded_cwd)
    set url_encoded_cwd (string replace -a '#' '%23' -- $url_encoded_cwd)
    set url_encoded_cwd (string replace -a '$' '%24' -- $url_encoded_cwd)
    set url_encoded_cwd (string replace -a '&' '%26' -- $url_encoded_cwd)
    set url_encoded_cwd (string replace -a "'" '%27' -- $url_encoded_cwd)
    printf '\e]7;file://%s%s\a' (hostname) $url_encoded_cwd
end

# OSC 133: Prompt marks
function _yoloterm_prompt_start
    printf '\e]133;A\a'
end

function _yoloterm_prompt_end
    printf '\e]133;B\a'
end

function _yoloterm_preexec --on-event fish_preexec
    # Command executed mark (with timestamp for duration calc)
    set -g _yoloterm_cmd_start (date +%s%3N)
    printf '\e]133;C\a'
end

function _yoloterm_precmd --on-event fish_prompt
    set -l exit_code $status
    
    # Command finished mark with exit code and duration
    if set -q _yoloterm_cmd_start
        set -l cmd_end (date +%s%3N)
        set -l duration (math $cmd_end - $_yoloterm_cmd_start)
        printf '\e]133;D;%d;%d\a' $exit_code $duration
        set -e _yoloterm_cmd_start
    else
        # No command was executed
        printf '\e]133;D;%d\a' $exit_code
    end
    
    # Report working directory
    _yoloterm_osc7
    
    # Prompt start for the next prompt
    _yoloterm_prompt_start
end

# Emit prompt start immediately
_yoloterm_prompt_start

# Add prompt end to your fish_prompt function:
#   function fish_prompt
#       echo -n (whoami)'@'(hostname)':'(prompt_pwd)'$ '
#       printf '\e]133;B\a'
#   end
