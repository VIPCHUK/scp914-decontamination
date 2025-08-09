using System;
using CommandSystem;

namespace ScpDecontamination.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DecontaminationCommand : ICommand
    {
        public string Command { get; } = "decontamination914";
        public string[] Aliases { get; } = { "d914" };
        public string Description { get; } = "Enables or disables SCP-914 decontamination for the current round.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count != 1)
            {
                response = "Usage: d914 [on/off]";
                return false;
            }

            string argument = arguments.At(0).ToLower();

            switch (argument)
            {
                case "on":
                    Plugin.Instance.SetDecontaminationDisabled(false);
                    response = "SCP-914 decontamination has been enabled for the round.";
                    return true;
                case "off":
                    Plugin.Instance.SetDecontaminationDisabled(true);
                    response = "SCP-914 decontamination has been disabled for the round.";
                    return true;
                default:
                    response = "Invalid argument. Usage: d914 [on/off]";
                    return false;
            }
        }
    }
}
