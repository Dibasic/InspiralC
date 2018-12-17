using System.Collections.Generic;

namespace inspiral
{
	internal partial class CommandModule : GameModule
	{
		internal void CmdAddrole(GameClient invoker, string invocation)
		{
			string[] tokens = invocation.ToLower().Split(" ");
			if(tokens.Length < 1)
			{
				invoker.SendLine("Who do you wish to view the roles of?");
				return;
			}
			else if(tokens.Length < 2)
			{
				invoker.SendLine("Which role do you wish to add?");
				return;
			}

			PlayerAccount acct = Game.Accounts.FindAccount(tokens[0].ToLower());
			if(acct == null)
			{
				invoker.SendLine($"Cannot find account for '{tokens[0]}'.");
				return;
			}

			GameRole role = Modules.Roles.GetRole(tokens[1].ToLower());
			if(role == null)
			{
				invoker.WriteLine($"Cannot find role for '{tokens[1]}'.");
			}
			else if(acct.roles.Contains(role))
			{
				invoker.WriteLine($"They already have that role.");
			}
			else
			{
				acct.roles.Add(role);
				Game.Accounts.QueueForUpdate(acct);
				invoker.WriteLine($"Added role '{role.name}' to '{acct.userName}'.");
			}
			invoker.SendPrompt();
		}
	}
}