﻿/*
	Copyright 2011 ForgeCraft team
	
	Dual-licensed under the	Educational Community License, Version 2.0 and
	the GNU General Public License, Version 3 (the "Licenses"); you may
	not use this file except in compliance with the Licenses. You may
	obtain a copy of the Licenses at
	
	http://www.opensource.org/licenses/ecl2.php
	http://www.gnu.org/licenses/gpl-3.0.html
	
	Unless required by applicable law or agreed to in writing,
	software distributed under the Licenses are distributed on an "AS IS"
	BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
	or implied. See the Licenses for the specific language governing
	permissions and limitations under the Licenses.
*/
using System;
using System.Text;
using System.Collections.Generic;

namespace SMP
{

    /// <summary>
    /// Handles packets.
    /// </summary>
	public partial class Player : System.IDisposable
    {
        #region blockchangehandler
        public delegate void BlockChangeHandler(Player p, int x, int y, int z, short type);
        public event BlockChangeHandler OnBlockChange;
        public void ClearBlockChange() { OnBlockChange = null; }
        #endregion
		#region Login
		private void HandleLogin(byte[] message)
		{
			int version = util.EndianBitConverter.Big.ToInt32(message, 0);
			short length = util.EndianBitConverter.Big.ToInt16(message, 4);
			if (length > 32) { Kick("Username too long"); return; }
			username = Encoding.BigEndianUnicode.GetString(message, 6, (length * 2));
			Server.Log(ip + " Logged in as " + username);
			Player.GlobalMessage(Color.Announce + username + " has joined the game!");
			
			if (version > Server.protocolversion)
            {
                Kick("Outdated server");
                return;
            }
            else if (version < Server.protocolversion)
            {
                Kick("Outdated client");
                return;
            }
			
			if (ip != "127.0.0.1")
			{
				if (Player.players.Count >= Server.MaxPlayers)
				{
					if (Server.useviplist && Server.VIPList.Contains(username.ToLower()))
					{
						for(int i = players.Count - 1; i >= 0; i--) // kick the last joined non-vip
						{
							if (!Server.VIPList.Contains(username.ToLower()))
							{
								players[i].Kick("You have been kicked for a VIP");
								break;
							}
						}
					}
					else if (Server.useviplist && !Server.VIPList.Contains(username.ToLower()))
					{
						Kick(Server.VIPListMessage);
					}
					else if (!Server.useviplist)
						Kick("Server is Full");	
				}
				
				if (Server.BanList.Contains(username.ToLower())) 
					Kick(Server.BanMessage);
				    
	            if (Server.usewhitelist && !Server.WhiteList.Contains(username.ToLower()))
					Kick(Server.WhiteListMessage);
			}
			
			//TODO: load Player attributes like group, and other settings
			LoadAttributes();
			
			LoggedIn = true;
			SendLoginPass();
			
			UpdateShi(this);
			
			if (PlayerAuth != null)
				PlayerAuth(this);
		}

        private void UpdateShi(Player p)
        {
            World w = World.Find(p.level.name);

            if (w.Israining)
            {
                w.rain(true);
               // p.SendMessage("IS IT RAINING?"); for debugging :P
            }
            
        }
		private void HandleHandshake(byte[] message)
		{
			//Server.Log("handshake-2");
			//short length = util.EndianBitConverter.Big.ToInt16(message, 0);
			//Server.Log(length + "");
			//Server.Log(Encoding.BigEndianUnicode.GetString(message, 2, length * 2));

			SendHandshake();
            
		}
		#endregion
		#region Chat
		private void HandleChatMessagePacket(byte[] message)
        {
			short length = util.EndianBitConverter.Big.ToInt16(message, 0);
			string m = Encoding.BigEndianUnicode.GetString(message, 2, length * 2);

            if (m.Length > 119)
            {
                Kick("Too many characters in message!");
                return;
            }
            foreach (char ch in m)
            {
                if (ch < 32 || ch >= 127)
                {
                    Kick("Illegal character in chat message!");
                    return;
                }
            }
            
            // Test for commands
            if (m[0] == '/') //in future maybe use config defined character
            {
                m = m.Remove(0, 1);

                int pos = m.IndexOf(' ');
                if (pos == -1)
                {
                    HandleCommand(m.ToLower(), "");
                    return;
                }

                string cmd = m.Substring(0, pos).ToLower();

                HandleCommand(cmd, m);
                return;
            }
			else if (m[0] == '@')
			{
				if(m[1] != ' ')
				{
					HandleCommand("msg", m.Substring(1));
				}
				else if(m[1] == ' ')
				{
					HandleCommand("msg", m.Substring(2));
				}
			}

            // TODO: Rank coloring
            //GlobalMessage(this.PlayerColor + "{1}§f: {2}", WrapMethod.Chat, this.Prefix, Username, message);
			if (!DoNotDisturb)
			{
				GlobalMessage(Color.DarkBlue + "<" + level.name + "> " + Color.Gray + "[" + group.GroupColor + group.Name + Color.Gray + "] " + this.GetColor() + GetName() + Color.White + ": " + m);
            	Server.ServerLogger.Log(LogLevel.Info, username + ": " + m);
			}
        }
		#endregion
		#region Movement stuffs
		private void HandlePlayerPacket(byte[] message)
		{
			try
			{
				byte onGround = message[0];

				if (onGround == onground)
					return;

				// TODO: Handle fall damage.

				onground = onGround;
			}
			catch (Exception e)
			{
				Server.Log(e.Message);
				Server.Log(e.StackTrace);
			}
		}
		private void HandlePlayerPositionPacket(byte[] message)
		{
			try
			{
				double x = util.EndianBitConverter.Big.ToDouble(message, 0);
				double y = util.EndianBitConverter.Big.ToDouble(message, 8);
				double stance = util.EndianBitConverter.Big.ToDouble(message, 16);
				double z = util.EndianBitConverter.Big.ToDouble(message, 24);
				byte onGround = message[32];

				// Return if position hasn't changed.
				if (new Point3(x, y, z) == pos && stance == Stance && onGround == onground)
					return;

				// Check stance
				if (stance - y < 0.1 || stance - y > 1.65)
				{
					Kick("Illegal Stance");
					return;
				}

				// Check position
				//if (Math.Abs(x - this.X) + Math.Abs(y - this.Y) + Math.Abs(z - this.Z) > 100)
				//{
				//    Kick("You moved to quickly :( (Hacking?)");
				//    return;
				//}
				/*else */
				if (Math.Abs(x) > 3.2E7D || Math.Abs(z) > 3.2E7D)
				{
					Kick("Illegal position");
					return;
				}

				//oldpos = pos;
				pos = new Point3(x, y, z);
				onground = onGround;

				e.UpdateChunks(false, false);
			}
			catch (Exception e)
			{
				Server.Log(e.Message);
				Server.Log(e.StackTrace);
			}
		}
		private void HandlePlayerLookPacket(byte[] message)
		{
			try
			{
				float yaw = util.EndianBitConverter.Big.ToSingle(message, 0);
				float pitch = util.EndianBitConverter.Big.ToSingle(message, 4);
				byte onGround = message[8];

				// Return if position hasn't changed.
				if (yaw == rot[0] && pitch == rot[1] && onGround == onground)
					return;

				rot[0] = yaw;
				rot[1] = pitch;
				onground = onGround;
			}
			catch (Exception e)
			{
				Server.Log(e.Message);
				Server.Log(e.StackTrace);
			}
		}
		private void HandlePlayerPositionAndLookPacket(byte[] message)
		{
			try
			{
				double x = util.EndianBitConverter.Big.ToDouble(message, 0);
				double y = util.EndianBitConverter.Big.ToDouble(message, 8);
				double stance = util.EndianBitConverter.Big.ToDouble(message, 16);
				double z = util.EndianBitConverter.Big.ToDouble(message, 24);
				float yaw = util.EndianBitConverter.Big.ToSingle(message, 32);
				float pitch = util.EndianBitConverter.Big.ToSingle(message, 36);
				byte onGround = message[40];

				// Return if position hasn't changed.
				if (new Point3(x, y, z) == pos && stance == Stance &&
					yaw == rot[0] && pitch == rot[1] && onGround == onground)
					return;

				// Check stance
				if (stance - y < 0.1 || stance - y > 1.65)
				{
					Kick("Illegal Stance");
					return;
				}

				// Check position
				//if (Math.Abs(x - this.X) + Math.Abs(y - this.Y) + Math.Abs(z - this.Z) > 100)
				//{
				//    Kick("You moved to quickly :( (Hacking?)");
				//    return;
				//}
				/*else */
				if (Math.Abs(x) > 3.2E7D || Math.Abs(z) > 3.2E7D)
				{
					Kick("Illegal position");
					return;
				}

				//oldpos = pos;
				pos = new Point3(x, y, z);
				rot[0] = yaw;
				rot[1] = pitch;
				onground = onGround;

				e.UpdateChunks(false, false);
			}
			catch (Exception e)
			{
				Server.Log(e.Message);
				Server.Log(e.StackTrace);
			}
		}
		#endregion
		#region BlockChanges
		private void HandleDigging(byte[] message)
		{
			if (message[0] == 0)
			{
				int x = util.EndianBitConverter.Big.ToInt32(message, 1);
				byte y = message[5];
				int z = util.EndianBitConverter.Big.ToInt32(message, 6);

				byte rc = level.GetBlock(x,y,z); //block hit
				if(BlockChange.LeftClicked.ContainsKey(rc))
				{
					BlockChange.LeftClicked[rc].DynamicInvoke(this, new BCS(new Point3(x, y, z), 0, 0, 0, 0));
				}

				// Send an animation to all nearby players.
                foreach( int i in VisibleEntities ) {
					Entity e = Entity.Entities[i];
					if (!e.isPlayer) continue;
					Player p = e.p;
                    if( p.level == level && p != this )
                        p.SendAnimation( id, 1 );
                }

                if (OnBlockChange != null)
                    OnBlockChange(this, x, y, z, rc);
			}
			if (message[0] == 2)
			{
				//Player is done digging
				int x = util.EndianBitConverter.Big.ToInt32(message, 1);
				byte y = message[5];
				int z = util.EndianBitConverter.Big.ToInt32(message, 6);

				short id = e.level.GetBlock(x, y, z);
				byte count = 1;

				if (BlockChange.Destroyed.ContainsKey(id))
				{
					if (!(bool)BlockChange.Destroyed[id].DynamicInvoke(this, new BCS(new Point3(x, y, z), 0, 0, 0, 0)))
					{
						Console.WriteLine("Delegate for " + id + " Destroyed returned false");
						return;
					}
				}

				id = BlockDropSwitch(id);

                if (id != 0)
                {
                    Item item = new Item(id, level) { count = count, meta = level.GetMeta(x, y, z), pos = new double[3] { x + .5, y + .5, z + .5 }, rot = new byte[3] { 1, 1, 1 }, OnGround = true };
                    item.e.UpdateChunks(false, false);
                }
				
				level.BlockChange(x, y, z, 0, 0);
			}
			if (message[0] == 4)
			{
				//Player dropped item in hand
			}
		}
		private void HandleBlockPlacementPacket(byte[] message)
		{
			int blockX = util.EndianBitConverter.Big.ToInt32(message, 0);
			byte blockY = message[4];
			int blockZ = util.EndianBitConverter.Big.ToInt32(message, 5);
			byte direction = message[9];

			if (blockX == -1 && blockZ == -1)
			{
				//this is supposed to just tell the server to update food and stuffs
				return;
			}

			short blockID = util.EndianBitConverter.Big.ToInt16(message, 10);

			byte amount = 0;
			short damage = 0;
			if (message.Length == 15)  //incase it is the secondary packet size
			{
				amount = message[11];
				damage = util.EndianBitConverter.Big.ToInt16(message, 12);
			}

			byte rc = level.GetBlock(blockX, blockY, blockZ);
			if (BlockChange.RightClickedOn.ContainsKey(rc))
			{
				if (!(bool)BlockChange.RightClickedOn[rc].DynamicInvoke(this, new BCS(new Point3(blockX, blockY, blockZ), blockID, direction, amount, damage)))
				{
					Console.WriteLine("Delegate for " + rc + " RightClickedON returned false");
					return;
				}
			}

			foreach (Entity e1 in new List<Entity>(Entity.Entities.Values))
			{
				Point3 block = new Point3(blockX, blockY, blockZ);
				Point3 pp = new Point3((int[])pos);

				if (block==pp)
				{
					//Server.Log("Entity found!");
					if (e1.isItem)
					{
						//move item
						continue;
					}
					if (e1.isObject)
					{
						//do stuff, like get in a minecart
						continue;
					}
					if (e1.isAI)
					{
						//do stuff, like shear sheep
						continue;
					}
					if (e1.isPlayer)
					{
						//dont do anything here? is there a case where you right click a player? a snowball maybe...
						//Check the players holding item, if they need to do something with it, do it.
						//anyway, if this is a player, then we dont place a block :D so return.
						return;
					}
				}
			}
			foreach (Entity e1 in new List<Entity>(Entity.Entities.Values))
			{
				Point3 block = new Point3(blockX, blockY, blockZ);
				Point3 pp = new Point3((int[])pos);
				pp.y--;

				if (block == pp)
				{
					if (e1.isPlayer)
					{
						//dont do anything here? is there a case where you right click a player? a snowball maybe...
						//we should do an item check, then return...
						//anyway, if this is a player, then we dont place a block :D so return.
						return;
					}
				}
			}

			switch (direction)
			{
				case 0: blockY--; break;
				case 1: blockY++; break;
				case 2: blockZ--; break;
				case 3: blockZ++; break;
				case 4: blockX--; break;
				case 5: blockX++; break;				
			}

            if (OnBlockChange != null)
                OnBlockChange(this, blockX, blockY, blockZ, blockID);

			if (blockID == -1)
			{
				//Players hand is empty
				//Player right clicked with empty hand!
				return;
			}

			if (blockID >= 1 && blockID <= 127)
			{
				if (BlockChange.Placed.ContainsKey(blockID))
				{
					if (!(bool)BlockChange.Placed[blockID].DynamicInvoke(this, new BCS(new Point3(blockX, blockY, blockZ), blockID, direction, amount, damage)))
					{
						return;
					}
				}
				level.BlockChange(blockX, (int)blockY, blockZ, (byte)blockID, (byte)damage);
				inventory.Remove(inventory.current_index, 1);
				return;
			}
			else
			{
				if(BlockChange.ItemRightClick.ContainsKey(blockID))
				{
					BlockChange.ItemRightClick[blockID].DynamicInvoke(this, new BCS(new Point3(blockX, blockY, blockZ), blockID, direction, amount, damage));
				}
				return;
			}
		}
		#endregion

		public void HandleHoldingChange(byte[] message)
		{
			try
			{
				current_slot_holding = (short)(util.EndianBitConverter.Big.ToInt16(message, 0) + 36);

				inventory.current_index = current_slot_holding;
				inventory.current_item = inventory.items[current_slot_holding];
				current_block_holding = inventory.current_item;
			}
			catch { }
		}
		
		public void HandleAnimation(byte[] message)
		{
			//TODO	
		}
		
		public void HandleWindowClose(byte[] message)
		{
			//TODO save the furnaces/dispensers, add unused stuff back to inventory etc
		}

		public void HandleWindowClick(byte[] message)
		{
			byte id = message[0];
			short slot = util.EndianBitConverter.Big.ToInt16(message, 1);
			ClickType click = (ClickType)message[3];
			short ActionID = util.EndianBitConverter.Big.ToInt16(message, 4);
			bool Shift = (message[6] == 1);
			short ItemID = util.EndianBitConverter.Big.ToInt16(message, 7);
			byte Count = 1;
			short Meta = 0;
			if (ItemID != -1)
			{
				Count = message[9];
				Meta = util.EndianBitConverter.Big.ToInt16(message, 10);
			}

			//TODO see if the player has anything in their mouseslot, if so then put that in the slot/switch if possible/add if needed)
			if (OnMouse != Item.Nothing)
			{
				if (OpenWindow)
				{

				}
				else
				{
					if (inventory.items[slot] != Item.Nothing)
					{

					}
					else
					{
						inventory.items[slot] = OnMouse;
						OnMouse = Item.Nothing;
					}
				}
			}
			else
			{
				if (OpenWindow)
				{

				}
				else
				{
					if (inventory.items[slot] != Item.Nothing)
					{
						OnMouse = inventory.items[slot];
						inventory.Remove(slot);
					}
					else
					{

					}
				}
			}
			
			
			//TODO if crafting, then check the current positioning
		}

		private void HandleDC(byte[] message)
		{
			Server.Log(username + " Disconnected.");
			GlobalMessage(username + " Left.");
			Disconnect();
			//TODO completely delete player.
		}
        public void HandleRespawn(byte[] message)
        {
            SendRespawn(message[0]);
        }
		public short BlockDropSwitch(short id)
		{
			switch (id)
			{
				case(1):
					return 4;
				case(2):
					return 3;
				case (7):
				case (8):
				case (9):
				case (10):
				case (11):
					return 0;
				case (13):
					if (Entity.random.Next(1, 10) == 5) return 318;
					return 13;
				case (16):
					return 263;
				case (18):
					return 0;
				case (20):
					return 0;
				case (21):
					return 251;
				case (31):
					if (Entity.random.Next(1, 5) == 3) return 295;
					return 0;
				case (32):
					return 0;
				case (34):
					return 0;
				case (36):
					return 0;
				case (51):
					return 0;
				case (52):
					return 0;
				case (55):
					return 331;
				case (56):
					return 264;
				case (63):
					return 323;
				case (68):
					return 323;
				case (75):
				case (76):
					return 75;
				case (79):
					return 0;
				case (90):
					return 0;
				case (92):
					return 0;
				case (93):
				case (94):
					return 354;

				default:
					return id;
			}
		}

		enum ClickType { LeftClick = 0, RightClick = 1 };
	}
}
