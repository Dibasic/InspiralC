using System.Data.SQLite;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System;
using System.Linq;
using Newtonsoft.Json;

namespace inspiral
{
	internal partial class ComponentModule : GameModule
	{
		internal List<GameComponent> Mobiles =>  GetComponents(Text.CompMobile);
	}

	internal static partial class Text
	{
		internal const string CompMobile = "mobile";
		internal const string FieldEnterMessage = "enter";
		internal const string FieldLeaveMessage = "leave";
		internal const string FieldDeathMessage = "death";
		internal const string FieldBodyplan =     "bodyplan";
		internal const string FieldRace =         "race";
		internal const string FieldBodypartList = "bodyparts";
	}

	internal class MobileBuilder : GameComponentBuilder
	{
		internal override List<string> editableFields { get; set; } = new List<string>() {Text.FieldEnterMessage, Text.FieldLeaveMessage, Text.FieldDeathMessage};
		internal override List<string> viewableFields { get; set; } = new List<string>() {Text.FieldEnterMessage, Text.FieldLeaveMessage, Text.FieldDeathMessage};
		internal override string Name         { get; set; } = Text.CompMobile;
		internal override string LoadSchema   { get; set; } = "SELECT * FROM components_mobile WHERE id = @p0;";
		internal override string TableSchema  { get; set; } = $@"components_mobile (
				id INTEGER NOT NULL PRIMARY KEY UNIQUE, 
				{Text.FieldEnterMessage} TEXT DEFAULT '', 
				{Text.FieldLeaveMessage} TEXT DEFAULT '', 
				{Text.FieldDeathMessage} TEXT DEFAULT '',
				{Text.FieldBodypartList} TEXT DEFAULT ''
				)";
		internal override string UpdateSchema   { get; set; } = $@"UPDATE components_mobile SET 
				{Text.FieldEnterMessage} = @p1, 
				{Text.FieldLeaveMessage} = @p2, 
				{Text.FieldDeathMessage} = @p3,
				{Text.FieldBodypartList} = @p4
				WHERE id = @p0";
		internal override string InsertSchema { get; set; } = $@"INSERT INTO components_mobile (
				id,
				{Text.FieldEnterMessage},
				{Text.FieldLeaveMessage},
				{Text.FieldDeathMessage},
				{Text.FieldBodypartList}
				) VALUES (
				@p0, 
				@p1, 
				@p2, 
				@p3, 
				@p4
				);";
		internal override GameComponent Build()
		{
			return new MobileComponent();
		}
	}

	internal class MobileComponent : GameComponent
	{
		internal string enterMessage = "A generic object enters from the $DIR.";
		internal string leaveMessage = "A generic object leaves to the $DIR.";
		internal string deathMessage = "A generic object lies here, dead.";
		internal string race =         "human";
		internal List<string> strikers = new List<string>();
		internal List<string> graspers = new List<string>();
		internal List<string> stance = new List<string>();
		internal List<string> equipmentSlots = new List<string>();

		internal Dictionary<string, GameObject> limbs = new Dictionary<string, GameObject>();
		internal override bool SetValue(string field, string newValue)
		{
			bool success = false;
			switch(field)
			{
				case Text.FieldEnterMessage:
					if(enterMessage != newValue)
					{
						enterMessage = newValue;
						success = true;
					}
					break;
				case Text.FieldLeaveMessage:
					if(leaveMessage != newValue)
					{
						leaveMessage = newValue;
						success = true;
					}
					break;
				case Text.FieldDeathMessage:
					if(deathMessage != newValue)
					{
						deathMessage = newValue;
						success = true;
					}
					break;
			}
			return success;
		}
		internal override string GetString(string field)
		{
			switch(field)
			{
				case Text.FieldEnterMessage:
					return enterMessage;
				case Text.FieldLeaveMessage:
					return leaveMessage;
				case Text.FieldDeathMessage:
					return deathMessage;
				case Text.FieldRace:
					return race;
				default:
					return null;
			}
		}
		internal bool CanMove()
		{
			return true;
		}
		internal override void ConfigureFromJson(JToken compData)
		{
			enterMessage = $"{parent.name} enters from the $DIR.";
			leaveMessage = $"{parent.name} leaves to the $DIR.";
			deathMessage = $"The corpse of {parent.name} lies here.";

			Bodyplan bp = null;
			if(!JsonExtensions.IsNullOrEmpty(compData["mobtype"]))
			{
				bp = Modules.Bodies.GetPlan((string)compData["mobtype"]);
			}
			if(bp == null)
			{
				bp = Modules.Bodies.GetPlan("humanoid");
			}

			foreach(Bodypart b in bp.allParts)
			{
				GameObject newLimb = (GameObject)Game.Objects.CreateNewInstance(false);
				newLimb.name = "limb";
				newLimb.aliases.Add("bodypart");

				VisibleComponent vis =   (VisibleComponent)newLimb.AddComponent(Text.CompVisible);
				vis.SetValue(Text.FieldShortDesc, b.name);
				vis.SetValue(Text.FieldRoomDesc, $"A severed {b.name} has been left here.");
				vis.SetValue(Text.FieldExaminedDesc, $"It is a severed {b.name} that has been lopped off its owner.");

				PhysicsComponent phys =  (PhysicsComponent)newLimb.AddComponent(Text.CompPhysics);
				phys.width =      b.width;
				phys.length =     b.length;
				phys.height =     b.height;
				phys.strikeArea = b.strikeArea;
				phys.edged =      b.isEdged;
				phys.UpdateValues();

				BodypartComponent body = (BodypartComponent)newLimb.AddComponent(Text.CompBodypart);
				body.canGrasp = b.canGrasp;
				body.canStand = b.canStand;
				body.isNaturalWeapon = b.isNaturalWeapon;
				
				foreach(string s in b.equipmentSlots)
				{
					if(!body.equipmentSlots.Contains(s))
					{
						body.equipmentSlots.Add(s);
					}
				}
				Game.Objects.AddDatabaseEntry(newLimb);
				limbs.Add(b.name, newLimb);
			}
			UpdateLists();
		}
		internal override void InstantiateFromRecord(SQLiteDataReader reader) 
		{
			enterMessage = reader[Text.FieldEnterMessage].ToString();
			leaveMessage = reader[Text.FieldLeaveMessage].ToString();
			deathMessage = reader[Text.FieldDeathMessage].ToString();
			foreach(KeyValuePair<string, long> limb in JsonConvert.DeserializeObject<Dictionary<string, long>>((string)reader[Text.FieldBodypartList]))
			{
				limbs.Add(limb.Key, (limb.Value != 0 ? (GameObject)Game.Objects.Get(limb.Value) : null));
			}
		}

		internal override void FinalizeObjectLoad()
		{
			UpdateLists();
		}
		internal void UpdateLists()
		{
			graspers.Clear();
			strikers.Clear();
			stance.Clear();
			equipmentSlots.Clear();
			foreach(KeyValuePair<string, GameObject> limb in limbs)
			{
				if(limb.Value != null)
				{
					BodypartComponent bp = (BodypartComponent)limb.Value.GetComponent(Text.CompBodypart);
					if(bp.canGrasp && !graspers.Contains(limb.Key))
					{
						graspers.Add(limb.Key);
					}
					if(bp.canStand && !stance.Contains(limb.Key))
					{
						stance.Add(limb.Key);
					}
					if(bp.isNaturalWeapon && !strikers.Contains(limb.Key))
					{
						strikers.Add(limb.Key);
					}
					foreach(string slot in bp.equipmentSlots)
					{
						if(!equipmentSlots.Contains(slot))
						{
							equipmentSlots.Add(slot);
						}
					}
				}
			}
		}
		internal override void AddCommandParameters(SQLiteCommand command) 
		{
			command.Parameters.AddWithValue("@p0", parent.id);
			command.Parameters.AddWithValue("@p1", enterMessage);
			command.Parameters.AddWithValue("@p2", leaveMessage);
			command.Parameters.AddWithValue("@p3", deathMessage);
			Dictionary<string, long> limbKeys = new Dictionary<string, long>();
			foreach(KeyValuePair<string, GameObject> limb in limbs)
			{
				limbKeys.Add(limb.Key, limb.Value != null ? limb.Value.id : 0);
			}
			command.Parameters.AddWithValue("@p4", JsonConvert.SerializeObject(limbKeys));
		}
		internal override string GetPrompt()
		{
			return $"{Colours.Fg("Pain:",Colours.Yellow)}{Colours.Fg("0%",Colours.BoldYellow)} {Colours.Fg("Bleed:",Colours.Red)}{Colours.Fg("0%",Colours.BoldRed)}";
		}
		internal string GetWeightedRandomBodypart()
		{
			return limbs.ElementAt(Game.rand.Next(0, limbs.Count)).Key;
		}
	}
	internal class BodypartData
	{
		internal int painAmount = 0;
		internal bool isAmputated = false;
		internal bool isBroken = false;
		internal List<Wound> wounds = new List<Wound>();
	}
	internal class Wound
	{
		internal int severity = 0;
		internal int bleedAmount = 0;
		internal bool isOpen = false;
	}
}