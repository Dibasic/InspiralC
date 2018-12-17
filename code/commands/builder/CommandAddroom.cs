using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace inspiral
{
	internal partial class CommandModule : GameModule
	{
		internal void CmdAddroom(GameClient invoker, string invocation)
		{
			if(invoker.shell == null || invoker.shell.location == null || !invoker.shell.location.HasComponent(Text.CompRoom))
			{
				invoker.WriteLine("This command is only usable within a room.");
			}
			else
			{
				string[] tokens = invocation.Split(" ");
				if(tokens.Length <= 0)
				{
					invoker.WriteLine("Which exit do you wish to add a room to?");
				}
				else if(tokens.Length <= 1)
				{
					invoker.WriteLine("Please specify a valid room ID to link to, or 'new' to use a new room.");
				}
				else
				{
					string exitToAdd = tokens[0].ToLower();
					if(Text.shortExits.ContainsKey(exitToAdd))
					{
						exitToAdd = Text.shortExits[exitToAdd];
					}
					RoomComponent room = (RoomComponent)invoker.shell.location.GetComponent(Text.CompRoom);
					if(room.exits.ContainsKey(exitToAdd))
					{
						invoker.WriteLine($"There is already an exit to the {exitToAdd} in this room.");
					}
					else
					{
						long roomId = -1;
						if(tokens[1].ToLower() == "new")
						{
							roomId = Modules.Templates.Instantiate("room").id;
						}
						else
						{
							try
							{
								roomId = Int32.Parse(tokens[1].ToLower());
							}
							catch(Exception e)
							{
								Debug.WriteLine($"Room ID exception: {e.ToString()}.");
							}
						}
						if(roomId == -1 || Game.Objects.Get(roomId) == null)
						{
							invoker.WriteLine("Please specify a valid room ID to link to, or 'new' to use a new room.");
						}
						else
						{
							bool saveEditedRoom = true;
							GameObject linkingRoom = (GameObject)Game.Objects.Get(roomId);
							if((tokens.Length >= 3 && tokens[2].ToLower() == "one-way") || !linkingRoom.HasComponent(Text.CompRoom) || !Text.reversedExits.ContainsKey(exitToAdd))
							{
								room.exits.Add(exitToAdd, roomId);
								saveEditedRoom = true;
								invoker.WriteLine($"You have connected {room.parent.id} to {roomId} via a one-way exit to the {exitToAdd}.");
							}
							else
							{
								string otherExit = Text.reversedExits[exitToAdd];
								RoomComponent otherRoom = (RoomComponent)linkingRoom.GetComponent(Text.CompRoom);
								if(otherRoom.exits.ContainsKey(otherExit))
								{
									room.exits.Add(exitToAdd, roomId);
									saveEditedRoom = true;
									invoker.WriteLine($"Target room already has an exit to the {otherExit}.\nYou have connected {room.parent.id} to {roomId} via a one-way exit to the {exitToAdd}.");
								}
								else
								{
									room.exits.Add(exitToAdd, roomId);
									saveEditedRoom = true;
									otherRoom.exits.Add(otherExit, room.parent.id);
									Game.Objects.QueueForUpdate(otherRoom.parent);
									invoker.WriteLine($"You have connected {room.parent.id} to {roomId} via a bidirectional exit to the {exitToAdd}.");
								}
							}
							if(saveEditedRoom)
							{
								Game.Objects.QueueForUpdate(room.parent);
							}
						}
					}
				}
			}
			invoker.SendPrompt();
		}
	}
}