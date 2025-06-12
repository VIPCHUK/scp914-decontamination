## Plugin Configuration:
> [!IMPORTANT]
> To disable or enable plugin, change value of parameter ```is_enabled``` in plugin config file(.yml). Use only boolean syntax words such as ```true``` or ```false```. Default config value is `True`:
```
is_enabled: true
```
> [!IMPORTANT]
> To change, which doorType is blocking, in decontamination sequence use parameter ```target_door_name```. Default target door name is `914`.
```
target_door_name: 914
```
> [!IMPORTANT]
> To change, start decontamination sequence cassie, you need to change parameter ```cassie_message_on_start```. With valid SCP:SL cassie syntax. (All legal words and phrases listed [here](https://steamcommunity.com/sharedfiles/filedetails/?id=1577299753))
```
cassie_message_on_start: 'pitch_0.15 .g4 .g4 pitch_1 ATTENTION CLOSED SCP DETECTED IN SCP 914 . .g3 AUTOMATIC DECONTAMINATION SEQUENCE ACTIVATED'
```
> [!IMPORTANT]
> To change, end of decontamination sequence cassie, you need to change parameter ```cassie_message_on_end```. With valid SCP:SL cassie syntax. (All legal words and phrases listed [here](https://steamcommunity.com/sharedfiles/filedetails/?id=1577299753))
```
cassie_message_on_end: 'SCP 914 DECONTAMINATION SEQUENCE COMPLETED . .g3 . ALL SYSTEMS NORMALIZE'
```
>[!IMPORTANT]
> To exlude, certain SCPSubject from plugin monitoring edit this parameter `excluded_scps`, in your config.
```
excluded_scps:
- Scp106
```
>[!IMPORTANT]
> To change delay between monitoring SCPs in SCP914, you need to change `check_interval`.
```
check_interval: 1
```
