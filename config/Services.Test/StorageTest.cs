﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.UIConfig.Services;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Exceptions;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.External;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Models;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Runtime;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services.Test.helpers;
using Xunit;

namespace Services.Test
{
    public class StorageTest
    {
        private readonly string azureMapsKey;
        private readonly Mock<IStorageAdapterClient> mockClient;
        private readonly Storage storage;
        private readonly Random rand;
        private const string PACKAGES_COLLECTION_ID = "packages";
        private const string EDGE_PACKAGE_JSON =
                @"{
                    ""id"": ""tempid"",
                    ""schemaVersion"": ""1.0"",
                    ""content"": {
                        ""modulesContent"": {
                        ""$edgeAgent"": {
                            ""properties.desired"": {
                            ""schemaVersion"": ""1.0"",
                            ""runtime"": {
                                ""type"": ""docker"",
                                ""settings"": {
                                ""loggingOptions"": """",
                                ""minDockerVersion"": ""v1.25""
                                }
                            },
                            ""systemModules"": {
                                ""edgeAgent"": {
                                ""type"": ""docker"",
                                ""settings"": {
                                    ""image"": ""mcr.microsoft.com/azureiotedge-agent:1.0"",
                                    ""createOptions"": ""{}""
                                }
                                },
                                ""edgeHub"": {
                                ""type"": ""docker"",
                                ""settings"": {
                                    ""image"": ""mcr.microsoft.com/azureiotedge-hub:1.0"",
                                    ""createOptions"": ""{}""
                                },
                                ""status"": ""running"",
                                ""restartPolicy"": ""always""
                                }
                            },
                            ""modules"": {}
                            }
                        },
                        ""$edgeHub"": {
                            ""properties.desired"": {
                            ""schemaVersion"": ""1.0"",
                            ""routes"": {
                                ""route"": ""FROM /messages/* INTO $upstream""
                            },
                            ""storeAndForwardConfiguration"": {
                                ""timeToLiveSecs"": 7200
                            }
                            }
                        }
                        }
                    },
                    ""targetCondition"": ""*"",
                    ""priority"": 30,
                    ""labels"": {
                        ""Name"": ""Test""
                    },
                    ""createdTimeUtc"": ""2018-08-20T18:05:55.482Z"",
                    ""lastUpdatedTimeUtc"": ""2018-08-20T18:05:55.482Z"",
                    ""etag"": null,
                    ""metrics"": {
                        ""results"": {},
                        ""queries"": {}
                    }
                 }";

        private const string ADM_PACKAGE_JSON =
                @"{
                    ""id"": ""9a9690df-f037-4c3a-8fc0-8eaba687609d"",
                    ""schemaVersion"": ""1.0"",
                    ""labels"": {
                        ""Type"": ""DeviceConfiguration"",
                        ""Name"": ""Deployment-12"",
                        ""DeviceGroupId"": ""MxChip"",
                        ""RMDeployment"": ""True""
                    },
                    ""content"": {
                        ""deviceContent"": {
                            ""properties.desired.firmware"": {
                                ""fwVersion"": ""1.0.1"",
                                ""fwPackageURI"": ""https://cs4c496459d5c79x44d1x97a.blob.core.windows.net/firmware/FirmwareOTA.ino.bin"",
                                ""fwPackageCheckValue"": ""45cd"",
                                ""fwSize"": 568648
                            }
                        }
                    },
                    ""targetCondition"": ""Tags.isVan1='Y'"",
                    ""createdTimeUtc"": ""2018-11-10T23:50:30.938Z"",
                    ""lastUpdatedTimeUtc"": ""2018-11-10T23:50:30.938Z"",
                    ""priority"": 20,
                    ""systemMetrics"": {
                        ""results"": {
                            ""targetedCount"": 2,
                            ""appliedCount"": 2
                        },
                        ""queries"": {
                            ""Targeted"": ""select deviceId from devices where Tags.isVan1='Y'"",
                            ""Applied"": ""select deviceId from devices where configurations.[[9a9690df-f037-4c3a-8fc0-8eaba687609d]].status = 'Applied'""
                        }
                    },
                    ""metrics"": {
                        ""results"": {},
                        ""queries"": {}
                    },
                    ""etag"": ""MQ==""
                    }";

        public StorageTest()
        {
            this.rand = new Random();

            this.azureMapsKey = this.rand.NextString();
            this.mockClient = new Mock<IStorageAdapterClient>();
            this.storage = new Storage(
                this.mockClient.Object,
                new ServicesConfig
                {
                    AzureMapsKey = this.azureMapsKey
                });
        }

        [Fact]
        public async Task GetThemeAsyncTest()
        {
            var name = this.rand.NextString();
            var description = this.rand.NextString();

            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(new
                    {
                        Name = name,
                        Description = description
                    })
                });

            var result = await this.storage.GetThemeAsync() as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.THEME_KEY)),
                    Times.Once);

            Assert.Equal(result.Name.ToString(), name);
            Assert.Equal(result.Description.ToString(), description);
            Assert.Equal(result.AzureMapsKey.ToString(), this.azureMapsKey);
        }

        [Fact]
        public async Task GetThemeAsyncDefaultTest()
        {
            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new ResourceNotFoundException());

            var result = await this.storage.GetThemeAsync() as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.THEME_KEY)),
                    Times.Once);

            Assert.Equal(result.Name.ToString(), Theme.Default.Name);
            Assert.Equal(result.Description.ToString(), Theme.Default.Description);
            Assert.Equal(result.AzureMapsKey.ToString(), this.azureMapsKey);
        }

        [Fact]
        public async Task SetThemeAsyncTest()
        {
            var name = this.rand.NextString();
            var description = this.rand.NextString();

            var theme = new
            {
                Name = name,
                Description = description
            };

            this.mockClient
                .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(theme)
                });

            var result = await this.storage.SetThemeAsync(theme) as dynamic;

            this.mockClient
                .Verify(x => x.UpdateAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.THEME_KEY),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(theme)),
                        It.Is<string>(s => s == "*")),
                    Times.Once);

            Assert.Equal(result.Name.ToString(), name);
            Assert.Equal(result.Description.ToString(), description);
            Assert.Equal(result.AzureMapsKey.ToString(), this.azureMapsKey);
        }

        [Fact]
        public async Task GetUserSettingAsyncTest()
        {
            var id = this.rand.NextString();
            var name = this.rand.NextString();
            var description = this.rand.NextString();

            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(new
                    {
                        Name = name,
                        Description = description
                    })
                });

            var result = await this.storage.GetUserSetting(id) as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.USER_COLLECTION_ID),
                        It.Is<string>(s => s == id)),
                    Times.Once);

            Assert.Equal(result.Name.ToString(), name);
            Assert.Equal(result.Description.ToString(), description);
        }

        [Fact]
        public async Task SetUserSettingAsyncTest()
        {
            var id = this.rand.NextString();
            var name = this.rand.NextString();
            var description = this.rand.NextString();

            var setting = new
            {
                Name = name,
                Description = description
            };

            this.mockClient
                .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(setting)
                });

            var result = await this.storage.SetUserSetting(id, setting) as dynamic;

            this.mockClient
                .Verify(x => x.UpdateAsync(
                        It.Is<string>(s => s == Storage.USER_COLLECTION_ID),
                        It.Is<string>(s => s == id),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(setting)),
                        It.Is<string>(s => s == "*")),
                    Times.Once);

            Assert.Equal(result.Name.ToString(), name);
            Assert.Equal(result.Description.ToString(), description);
        }

        [Fact]
        public async Task GetLogoShouldReturnExpectedLogo()
        {
            var image = this.rand.NextString();
            var type = this.rand.NextString();

            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(new Logo
                    {
                        Image = image,
                        Type = type,
                        IsDefault = false
                    })
                });

            var result = await this.storage.GetLogoAsync() as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.LOGO_KEY)),
                    Times.Once);

            Assert.Equal(image, result.Image.ToString());
            Assert.Equal(type, result.Type.ToString());
            Assert.Null(result.Name);
            Assert.False(result.IsDefault);
        }

        [Fact]
        public async Task GetLogoShouldReturnExpectedLogoAndName()
        {
            var image = this.rand.NextString();
            var type = this.rand.NextString();
            var name = this.rand.NextString();

            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(new Logo
                    {
                        Image = image,
                        Type = type,
                        Name = name,
                        IsDefault = false
                    })
                });

            var result = await this.storage.GetLogoAsync() as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.LOGO_KEY)),
                    Times.Once);

            Assert.Equal(image, result.Image.ToString());
            Assert.Equal(type, result.Type.ToString());
            Assert.Equal(name, result.Name.ToString());
            Assert.False(result.IsDefault);
        }

        [Fact]
        public async Task GetLogoShouldReturnDefaultLogoOnException()
        {
            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new ResourceNotFoundException());

            var result = await this.storage.GetLogoAsync() as dynamic;

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.LOGO_KEY)),
                    Times.Once);

            Assert.Equal(Logo.Default.Image, result.Image.ToString());
            Assert.Equal(Logo.Default.Type, result.Type.ToString());
            Assert.Equal(Logo.Default.Name, result.Name.ToString());
            Assert.True(result.IsDefault);
        }

        [Fact]
        public async Task SetLogoShouldNotOverwriteOldNameWithNull()
        {
            var image = this.rand.NextString();
            var type = this.rand.NextString();

            var oldImage = this.rand.NextString();
            var oldType = this.rand.NextString();
            var oldName = this.rand.NextString();

            var logo = new Logo
            {
                Image = image,
                Type = type
            };
            
            Logo result = await SetLogoHelper(logo, oldImage, oldName, oldType, false);

            this.mockClient
                .Verify(x => x.UpdateAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.LOGO_KEY),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(logo)),
                        It.Is<string>(s => s == "*")),
                    Times.Once);

            Assert.Equal(image, result.Image.ToString());
            Assert.Equal(type, result.Type.ToString());
            // If name is not set, old name should remain
            Assert.Equal(oldName, result.Name.ToString());
            Assert.False(result.IsDefault);
        }

        [Fact]
        public async Task SetLogoShouldSetAllPartsOfLogoIfNotNull()
        {
            var image = this.rand.NextString();
            var type = this.rand.NextString();
            var name = this.rand.NextString();

            var oldImage = this.rand.NextString();
            var oldType = this.rand.NextString();
            var oldName = this.rand.NextString();

            var logo = new Logo
            {
                Image = image,
                Type = type,
                Name = name
            };

            Logo result = await SetLogoHelper(logo, oldImage, oldName, oldType, false);

            Assert.Equal(image, result.Image.ToString());
            Assert.Equal(type, result.Type.ToString());
            Assert.Equal(name, result.Name.ToString());
            Assert.False(result.IsDefault);
        }

        [Fact]
        public async Task GetAllDeviceGroupsAsyncTest()
        {
            var groups = new[]
            {
                new DeviceGroup
                {
                    DisplayName = this.rand.NextString(),
                    Conditions = new List<DeviceGroupCondition>()
                    {
                        new DeviceGroupCondition()
                        {
                            Key = this.rand.NextString(),
                            Operator = OperatorType.EQ,
                            Value = this.rand.NextString()
                        }
                    }
                },
                new DeviceGroup
                {
                    DisplayName = this.rand.NextString(),
                    Conditions = new List<DeviceGroupCondition>()
                    {
                        new DeviceGroupCondition()
                        {
                            Key = this.rand.NextString(),
                            Operator = OperatorType.EQ,
                            Value = this.rand.NextString()
                        }
                    }
                },
                new DeviceGroup
                {
                    DisplayName = this.rand.NextString(),
                    Conditions = new List<DeviceGroupCondition>()
                    {
                        new DeviceGroupCondition()
                        {
                            Key = this.rand.NextString(),
                            Operator = OperatorType.EQ,
                            Value = this.rand.NextString()
                        }
                    }
                }
            };

            var items = groups.Select(g => new ValueApiModel
            {
                Key = this.rand.NextString(),
                Data = JsonConvert.SerializeObject(g),
                ETag = this.rand.NextString()
            }).ToList();

            this.mockClient
                .Setup(x => x.GetAllAsync(It.IsAny<string>()))
                .ReturnsAsync(new ValueListApiModel { Items = items });

            var result = (await this.storage.GetAllDeviceGroupsAsync()).ToList();

            this.mockClient
                .Verify(x => x.GetAllAsync(
                        It.Is<string>(s => s == Storage.DEVICE_GROUP_COLLECTION_ID)),
                    Times.Once);

            Assert.Equal(result.Count, groups.Length);
            foreach (var g in result)
            {
                var item = items.Single(i => i.Key == g.Id);
                var group = JsonConvert.DeserializeObject<DeviceGroup>(item.Data);
                Assert.Equal(g.DisplayName, group.DisplayName);
                Assert.Equal(g.Conditions.First().Key, group.Conditions.First().Key);
                Assert.Equal(g.Conditions.First().Operator, group.Conditions.First().Operator);
                Assert.Equal(g.Conditions.First().Value, group.Conditions.First().Value);
            }
        }

        [Fact]
        public async Task GetDeviceGroupsAsyncTest()
        {
            var groupId = this.rand.NextString();
            var displayName = this.rand.NextString();
            var conditions = new List<DeviceGroupCondition>()
            {
                new DeviceGroupCondition()
                {
                    Key = this.rand.NextString(),
                    Operator = OperatorType.EQ,
                    Value = this.rand.NextString()
                }
            };
            var etag = this.rand.NextString();

            this.mockClient
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = groupId,
                    Data = JsonConvert.SerializeObject(new DeviceGroup
                    {
                        DisplayName = displayName,
                        Conditions = conditions
                    }),
                    ETag = etag
                });

            var result = await this.storage.GetDeviceGroupAsync(groupId);

            this.mockClient
                .Verify(x => x.GetAsync(
                        It.Is<string>(s => s == Storage.DEVICE_GROUP_COLLECTION_ID),
                        It.Is<string>(s => s == groupId)),
                    Times.Once);

            Assert.Equal(result.DisplayName, displayName);
            Assert.Equal(result.Conditions.First().Key, conditions.First().Key);
            Assert.Equal(result.Conditions.First().Operator, conditions.First().Operator);
            Assert.Equal(result.Conditions.First().Value, conditions.First().Value);
        }

        [Fact]
        public async Task CreateDeviceGroupAsyncTest()
        {
            var groupId = this.rand.NextString();
            var displayName = this.rand.NextString();
            var conditions = new List<DeviceGroupCondition>()
            {
                new DeviceGroupCondition()
                {
                    Key = this.rand.NextString(),
                    Operator = OperatorType.EQ,
                    Value = this.rand.NextString()
                }
            };
            var etag = this.rand.NextString();

            var group = new DeviceGroup
            {
                DisplayName = displayName,
                Conditions = conditions
            };

            this.mockClient
                .Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = groupId,
                    Data = JsonConvert.SerializeObject(group),
                    ETag = etag
                });

            var result = await this.storage.CreateDeviceGroupAsync(group);

            this.mockClient
                .Verify(x => x.CreateAsync(
                        It.Is<string>(s => s == Storage.DEVICE_GROUP_COLLECTION_ID),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(group, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }))),
                    Times.Once);

            Assert.Equal(result.Id, groupId);
            Assert.Equal(result.DisplayName, displayName);
            Assert.Equal(result.Conditions.First().Key, conditions.First().Key);
            Assert.Equal(result.Conditions.First().Operator, conditions.First().Operator);
            Assert.Equal(result.Conditions.First().Value, conditions.First().Value);
            Assert.Equal(result.ETag, etag);
        }

        [Fact]
        public async Task UpdateDeviceGroupAsyncTest()
        {
            var groupId = this.rand.NextString();
            var displayName = this.rand.NextString();
            var conditions = new List<DeviceGroupCondition>()
            {
                new DeviceGroupCondition()
                {
                    Key = this.rand.NextString(),
                    Operator = OperatorType.EQ,
                    Value = this.rand.NextString()
                }
            };
            var etagOld = this.rand.NextString();
            var etagNew = this.rand.NextString();

            var group = new DeviceGroup
            {
                DisplayName = displayName,
                Conditions = conditions
            };

            this.mockClient
                .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = groupId,
                    Data = JsonConvert.SerializeObject(group),
                    ETag = etagNew
                });

            var result = await this.storage.UpdateDeviceGroupAsync(groupId, group, etagOld);

            this.mockClient
                .Verify(x => x.UpdateAsync(
                        It.Is<string>(s => s == Storage.DEVICE_GROUP_COLLECTION_ID),
                        It.Is<string>(s => s == groupId),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(group, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })),
                        It.Is<string>(s => s == etagOld)),
                    Times.Once);

            Assert.Equal(result.Id, groupId);
            Assert.Equal(result.DisplayName, displayName);
            Assert.Equal(result.Conditions.First().Key, conditions.First().Key);
            Assert.Equal(result.Conditions.First().Operator, conditions.First().Operator);
            Assert.Equal(result.Conditions.First().Value, conditions.First().Value);
            Assert.Equal(result.ETag, etagNew);
        }

        [Fact]
        public async Task DeleteDeviceGroupAsyncTest()
        {
            var groupId = this.rand.NextString();

            this.mockClient
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            await this.storage.DeleteDeviceGroupAsync(groupId);

            this.mockClient
                .Verify(x => x.DeleteAsync(
                        It.Is<string>(s => s == Storage.DEVICE_GROUP_COLLECTION_ID),
                        It.Is<string>(s => s == groupId)),
                    Times.Once);
        }

        private async Task<Logo> SetLogoHelper(Logo logo, string oldImage, string oldName, string oldType, bool isDefault)
        {
            this.mockClient
                .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string id, string key, string value, string etag) => new ValueApiModel
                {
                    Data = value
                });

            this.mockClient.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ValueApiModel
                {
                    Data = JsonConvert.SerializeObject(new Logo
                    {
                        Image = oldImage,
                        Type = oldType,
                        Name = oldName,
                        IsDefault = false
                    })
                });

            Logo result = await this.storage.SetLogoAsync(logo);

            this.mockClient
                .Verify(x => x.UpdateAsync(
                        It.Is<string>(s => s == Storage.SOLUTION_COLLECTION_ID),
                        It.Is<string>(s => s == Storage.LOGO_KEY),
                        It.Is<string>(s => s == JsonConvert.SerializeObject(logo)),
                        It.Is<string>(s => s == "*")),
                    Times.Once);

            return result;
        }

        [Fact]
        public async Task AddEdgePackageTest()
        {
            // Arrange
            const string collectionId = "packages";
            const string key = "package name";
            var pkg = new Package
            {
                Id = string.Empty,
                Name = key,
                Type = PackageType.EdgeManifest,
                ConfigType = string.Empty,
                Content = EDGE_PACKAGE_JSON
            };
            var value = JsonConvert.SerializeObject(pkg);

            this.mockClient
                .Setup(x => x.CreateAsync(
                       It.Is<string>(i => i == collectionId),
                       It.Is<string>(i => this.IsMatchingPackage(i, value))))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = key,
                    Data = value
                });

            // Act
            var result = await this.storage.AddPackageAsync(pkg);

            // Assert
            Assert.Equal(pkg.Name, result.Name);
            Assert.Equal(pkg.Type, result.Type);
            Assert.Equal(pkg.Content, result.Content);
        }

        [Fact]
        public async Task AddADMPackageTest()
        {
            // Arrange
            const string collectionId = "packages";
            const string key = "package name";
            var pkg = new Package
            {
                Id = string.Empty,
                Name = key,
                Type = PackageType.DeviceConfiguration,
                Content = ADM_PACKAGE_JSON,
                ConfigType = ConfigType.FirmwareUpdateMxChip.ToString()
            };
            var value = JsonConvert.SerializeObject(pkg);

            this.mockClient
                .Setup(x => x.CreateAsync(
                       It.Is<string>(i => i == collectionId),
                       It.Is<string>(i => this.IsMatchingPackage(i, value))))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = key,
                    Data = value
                });

            // Act
            var result = await this.storage.AddPackageAsync(pkg);

            // Assert
            Assert.Equal(pkg.Name, result.Name);
            Assert.Equal(pkg.Type, result.Type);
            Assert.Equal(pkg.Content, result.Content);
            Assert.Equal(pkg.ConfigType, result.ConfigType);
        }

        [Fact]
        public async Task AddADMCustomPackageTest()
        {
            // Arrange
            const string collectionId = "packages";
            const string key = "package name";
            const string customConfig = "Custom-config";

            var pkg = new Package
            {
                Id = string.Empty,
                Name = key,
                Type = PackageType.DeviceConfiguration,
                Content = ADM_PACKAGE_JSON,
                ConfigType = "Custom-config"
            };
            var value = JsonConvert.SerializeObject(pkg);

            this.mockClient
                .Setup(x => x.CreateAsync(
                       It.Is<string>(i => i == collectionId),
                       It.Is<string>(i => this.IsMatchingPackage(i, value))))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = key,
                    Data = value
                });
            const string configKey = "configurations";

            this.mockClient
                .Setup(x => x.UpdateAsync(
                       It.Is<string>(i => i == collectionId),
                       It.Is<string>(i => i == configKey),
                       It.Is<string>(i => i == customConfig),
                       It.Is<string>(i => i == "*")))
                .ReturnsAsync(new ValueApiModel
                {
                    Key = key,
                    Data = value
                });

            this.mockClient
                .Setup(x => x.GetAsync(
                    It.Is<string>(i => i == collectionId),
                    It.Is<string>(i => i == configKey)))
                .ThrowsAsync(new ResourceNotFoundException());

            // Act
            var result = await this.storage.AddPackageAsync(pkg);

            // Assert
            Assert.Equal(pkg.Name, result.Name);
            Assert.Equal(pkg.Type, result.Type);
            Assert.Equal(pkg.Content, result.Content);
            Assert.Equal(pkg.ConfigType, result.ConfigType);
        }

        [Fact]
        public async Task ListConfigurationsTest()
        {
            const string collectionId = "packages";
            const string configKey = "configurations";

            // Arrange
            this.mockClient
                .Setup(x => x.GetAsync(
                    It.Is<string>(i => i == collectionId),
                    It.Is<string>(i => i == configKey)))
                .ThrowsAsync(new ResourceNotFoundException());

            // Act
            var result = await this.storage.GetAllConfigurationsAsync();

            // Assert
            Assert.Empty(result.configurations);
        }


        [Fact]
        public async Task InvalidPackageThrowsTest()
        {
            // Arrange
            var pkg = new Package
            {
                Id = string.Empty,
                Name = "testpackage",
                Type = PackageType.EdgeManifest,
                Content = "InvalidPackage"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidInputException>(async () => 
                await this.storage.AddPackageAsync(pkg));
        }

        [Fact]
        public async Task DeletePackageAsyncTest()
        {
            // Arrange
            var packageId = this.rand.NextString();

            this.mockClient
                .Setup(x => x.DeleteAsync(It.Is<string>(s => s == PACKAGES_COLLECTION_ID),
                                          It.Is<string>(s => s == packageId)))
                .Returns(Task.FromResult(0));

            // Act
            await this.storage.DeletePackageAsync(packageId);

            // Assert
            this.mockClient
                .Verify(x => x.DeleteAsync(
                        It.Is<string>(s => s == PACKAGES_COLLECTION_ID),
                        It.Is<string>(s => s == packageId)),
                    Times.Once);
        }

        private bool IsMatchingPackage(string pkgJson, string originalPkgJson)
        {
            const string dateCreatedField = "DateCreated";
            var createdPkg = JObject.Parse(pkgJson);
            var originalPkg = JObject.Parse(originalPkgJson);

            // To prevent false failures on unit tests we allow a couple of seconds diffence
            // when verifying the date created.
            var dateCreated = DateTimeOffset.Parse(createdPkg[dateCreatedField].ToString());
            var secondsDiff = (DateTimeOffset.UtcNow - dateCreated).TotalSeconds;
            if (secondsDiff > 3)
            {
                return false;
            }

            createdPkg.Remove(dateCreatedField);
            originalPkg.Remove(dateCreatedField);

            return JToken.DeepEquals(createdPkg, originalPkg);
        }
    }
}
