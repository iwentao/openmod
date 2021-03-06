﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenMod.API;
using OpenMod.API.Ioc;
using OpenMod.API.Persistence;
using OpenMod.API.Prioritization;
using OpenMod.API.Users;
using OpenMod.Common.Helpers;
using OpenMod.Core.Helpers;

namespace OpenMod.Core.Users
{
    [UsedImplicitly]
    [ServiceImplementation(Lifetime = ServiceLifetime.Singleton, Priority = Priority.Lowest)]
    public class UserDataStore : IUserDataStore, IAsyncDisposable
    {
        private readonly ILogger<UserDataStore> m_Logger;
        private readonly IRuntime m_Runtime;
        public const string UsersKey = "users";
        private readonly IDataStore m_DataStore;
        private UsersData m_CachedUsersData;
        private IDisposable m_FileChangeWatcher;
        private bool m_IsUpdating;


        public UserDataStore(
            ILogger<UserDataStore> logger,
            IOpenModDataStoreAccessor dataStoreAccessor,
            IRuntime runtime)
        {
            m_Logger = logger;
            m_Runtime = runtime;
            m_DataStore = dataStoreAccessor.DataStore;

            // suppress errors because the compiler can't analyze that the values are set from the statements below
            m_CachedUsersData = null!;
            m_FileChangeWatcher = null!;

            AsyncHelper.RunSync(async () =>
            {
                m_CachedUsersData = await EnsureUserDataCreatedAsync();
            });
        }

        private async Task<UsersData> EnsureUserDataCreatedAsync()
        {
            var created = false;
            if (!await m_DataStore.ExistsAsync(UsersKey))
            {
                m_CachedUsersData = new UsersData { Users = GetDefaultUsersData() };

                await m_DataStore.SaveAsync(UsersKey, m_CachedUsersData);
                created = true;
            }

            m_FileChangeWatcher = m_DataStore.AddChangeWatcher(UsersKey, m_Runtime, () =>
            {
                if (!m_IsUpdating)
                {
                    m_CachedUsersData = AsyncHelper.RunSync(LoadUsersDataFromDiskAsync);
                }

                m_IsUpdating = false;
            });

            if (!created)
            {
                m_CachedUsersData = await LoadUsersDataFromDiskAsync();
            }

            return m_CachedUsersData;
        }

        public async Task<UserData?> GetUserDataAsync(string userId, string userType)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(nameof(userId));
            }

            if (string.IsNullOrEmpty(userType))
            {
                throw new ArgumentException(nameof(userType));
            }

            var usersData = await GetUsersDataAsync();
            return usersData.Users?.FirstOrDefault(d => (d.Type?.Equals(userType, StringComparison.OrdinalIgnoreCase) ?? false)
                                                        && (d.Id?.Equals(userId, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        public async Task<T?> GetUserDataAsync<T>(string userId, string userType, string key)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(nameof(userId));
            }

            if (string.IsNullOrEmpty(userType))
            {
                throw new ArgumentException(nameof(userType));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(nameof(key));
            }

            var data = await GetUserDataAsync(userId, userType) ?? new UserData();
            if (data.Data == null)
            {
                return default;
            }

            if (!data.Data.ContainsKey(key))
            {
                return default;
            }

            var dataObject = data.Data[key];
            if (dataObject is T obj)
            {
                return obj;
            }

            if (dataObject == null)
            {
                return default;
            }

            if (dataObject.GetType().HasConversionOperator(typeof(T)))
            {
                return (T)dataObject;
            }

            if (dataObject is Dictionary<object, object> dict)
            {
                return dict.ToObject<T>();
            }

            throw new Exception($"Failed to parse {dataObject.GetType()} as {typeof(T)}");
        }

        public async Task SetUserDataAsync<T>(string userId, string userType, string key, T? value)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(nameof(userId));
            }

            if (string.IsNullOrEmpty(userType))
            {
                throw new ArgumentException(nameof(userType));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(nameof(key));
            }

            var userData = await GetUserDataAsync(userId, userType) ?? new UserData();
            userData.Data ??= new();

            if (userData.Data.ContainsKey(key))
            {
                userData.Data.Remove(key);
            }

            userData.Data.Add(key, value);
            await SetUserDataAsync(userData);
        }

        public async Task<IReadOnlyCollection<UserData>> GetUsersDataAsync(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException(nameof(type));
            }

            var usersData = await GetUsersDataAsync();
            return usersData.Users?
                       .Where(d => d.Type?.Equals(type, StringComparison.OrdinalIgnoreCase) ?? false)
                       .ToList()
                ?? new List<UserData>();
        }

        public async Task SetUserDataAsync(UserData userData)
        {
            if (userData == null)
            {
                throw new ArgumentNullException(nameof(userData));
            }

            var usersData = await GetUsersDataAsync();
            usersData.Users ??= GetDefaultUsersData();

            var idx = usersData.Users.FindIndex(c =>
                (c.Type?.Equals(userData.Type, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (c.Id?.Equals(userData.Id, StringComparison.OrdinalIgnoreCase) ?? false));

            usersData.Users.RemoveAll(c =>
                (c.Type?.Equals(userData.Type, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (c.Id?.Equals(userData.Id, StringComparison.OrdinalIgnoreCase) ?? false));

            // preserve location in data
            if (idx >= 0)
            {
                usersData.Users.Insert(idx, userData);
            }
            else
            {
                usersData.Users.Add(userData);
            }

            m_CachedUsersData = usersData;
            m_IsUpdating = true;

            await m_DataStore.SaveAsync(UsersKey, m_CachedUsersData);
        }

        private List<UserData> GetDefaultUsersData()
        {
            return new()
            {
                new()
                {
                    FirstSeen = null,
                    LastSeen = null,
                    LastDisplayName = "root",
                    Id = "root",
                    Type = KnownActorTypes.Rcon,
                    Data = new Dictionary<string, object?>(),
                    Permissions = new HashSet<string> { "*" },
                    Roles = new HashSet<string>()
                }
            };
        }

        private Task<UsersData> GetUsersDataAsync()
        {
            return Task.FromResult(m_CachedUsersData);
        }

        private async Task<UsersData> LoadUsersDataFromDiskAsync()
        {
            if (!await m_DataStore.ExistsAsync(UsersKey))
            {
                m_CachedUsersData = new UsersData
                {
                    Users = GetDefaultUsersData()
                };

                await m_DataStore.SaveAsync(UsersKey, m_CachedUsersData);
                return m_CachedUsersData;
            }

            return await m_DataStore.LoadAsync<UsersData>(UsersKey) ?? new UsersData
            {
                Users = GetDefaultUsersData()
            };
        }

        public async ValueTask DisposeAsync()
        {
            m_FileChangeWatcher.Dispose();

            if (m_CachedUsersData.Users == null)
            {
                throw new Exception("Tried to save null users data");
            }

            await m_DataStore.SaveAsync(UsersKey, m_CachedUsersData);
        }
    }
}
