using System.Collections.Generic;
using System.Text;
using KindredCommands.Commands.Converters;
using KindredCommands.Data;
using ProjectM;
using ProjectM.Network;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;

namespace KindredCommands.Commands;
internal class GodCommands
{
	const int DEFAULT_FAST_SPEED = 15;

	[Command("god", adminOnly: true)]
	public static void GodCommand(ChatCommandContext ctx, OnlinePlayer player = null)
	{
		var userEntity = player?.Value.UserEntity ?? ctx.Event.SenderUserEntity;
		var charEntity = player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity;

		Core.BoostedPlayerService.RemoveBoostedPlayer(charEntity);
		Core.BoostedPlayerService.SetAttackSpeedMultiplier(charEntity, 10f);
		Core.BoostedPlayerService.SetDamageBoost(charEntity, 10000f);
		Core.BoostedPlayerService.SetHealthBoost(charEntity, 100000);
		Core.BoostedPlayerService.SetSpeedBoost(charEntity, DEFAULT_FAST_SPEED);
		Core.BoostedPlayerService.SetYieldMultiplier(charEntity, 10f);
		Core.BoostedPlayerService.ToggleBatVision(charEntity);
		Core.BoostedPlayerService.ToggleNoAggro(charEntity);
		Core.BoostedPlayerService.ToggleNoBlooddrain(charEntity);
		Core.BoostedPlayerService.ToggleNoCooldown(charEntity);
		Core.BoostedPlayerService.ToggleNoDurability(charEntity);
		Core.BoostedPlayerService.TogglePlayerImmaterial(charEntity);
		Core.BoostedPlayerService.TogglePlayerInvincible(charEntity);
		Core.BoostedPlayerService.TogglePlayerShrouded(charEntity);
		Core.BoostedPlayerService.UpdateBoostedPlayer(charEntity);

		var name = player?.Value.UserEntity.Read<User>().CharacterName ?? ctx.Event.User.CharacterName;
		ctx.Reply($"Godmode setado para <color=white>{name}</color>");
	}

	[Command("ungod", adminOnly: true)]
	public static void MortalCommand(ChatCommandContext ctx, OnlinePlayer player = null)
	{
		var charEntity = (player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity);

		if (!Core.BoostedPlayerService.IsBoostedPlayer(charEntity) && !BuffUtility.HasBuff(Core.EntityManager, charEntity, Prefabs.BoostedBuff1)) return;

		Core.BoostedPlayerService.RemoveBoostedPlayer(charEntity);

		var name = player?.Value.UserEntity.Read<User>().CharacterName ?? ctx.Event.User.CharacterName;
		ctx.Reply($"Godmode e boosts removidos de <color=white>{name}</color>");
		
			}
		}
