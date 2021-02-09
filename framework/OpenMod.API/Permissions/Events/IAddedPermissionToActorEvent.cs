﻿namespace OpenMod.API.Permissions.Events
{
    public interface IAddedPermissionToActorEvent : IPermissionActorEvent
    {
        PermissionType PermissionType { get; }

        string Permission { get; }
    }
}
