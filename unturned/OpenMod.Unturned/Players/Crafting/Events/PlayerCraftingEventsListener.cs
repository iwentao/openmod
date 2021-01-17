﻿using OpenMod.API;
using OpenMod.API.Eventing;
using OpenMod.API.Users;
using OpenMod.Unturned.Events;
using SDG.Unturned;
// ReSharper disable DelegateSubtraction

namespace OpenMod.Unturned.Players.Crafting.Events
{
    internal class CraftingEventsListener : UnturnedEventsListener
    {
        public CraftingEventsListener(IOpenModHost openModHost,
            IEventBus eventBus,
            IUserManager userManager) : base(openModHost, eventBus, userManager)
        {
        }

        public override void Subscribe()
        {
            PlayerCrafting.onCraftBlueprintRequested += OnCraftBlueprintRequested;
        }

        public override void Unsubscribe()
        {
            PlayerCrafting.onCraftBlueprintRequested -= OnCraftBlueprintRequested;
        }

        private void OnCraftBlueprintRequested(PlayerCrafting crafting, ref ushort itemId, ref byte blueprintIndex, ref bool shouldAllow)
        {
            var player = GetUnturnedPlayer(crafting.player);

            var @event = new UnturnedPlayerCraftingEvent(player, itemId, blueprintIndex)
            {
                IsCancelled = !shouldAllow
            };

            Emit(@event);

            itemId = @event.ItemId;
            blueprintIndex = @event.BlueprintIndex;
            shouldAllow = !@event.IsCancelled;
        }
    }
}
