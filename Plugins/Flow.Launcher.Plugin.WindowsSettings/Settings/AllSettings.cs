namespace Flow.Launcher.Plugin.WindowsSettings.Settings;

public static class AllSettings
{
    public static readonly SettingsPage Settings = new()
    {
        Type = SettingType.SettingsApp,
        Name = "Settings",
        Command = "ms-settings:",

        Settings = [
            // System
            new SettingsPage()
            {
                Type = SettingType.SettingsApp,
                Name = "System",
                Command = "ms-settings:system",

                Settings = [

                    // Display
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Display",
                        Command = "ms-settings:display",
                        Glyph = "\ue7f4",

                        AlternativeNames = ["Screen", "Color profile"],

                        Settings = [
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Night light",
                                Command = "ms-settings:nightlight",
                                Glyph = "\uf08c",

                                AlternativeNames = ["Blue light"]
                            },
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "HDR",
                                Command = "ms-settings:display-hdr"
                            },
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Advanced display settings",
                                Command = "ms-settings:advanceddisplay",

                                AlternativeNames = ["Advanced screen settings"]
                            },
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Graphics settings",
                                Command = "ms-settings:display-advancedgraphics",

                                AlternativeNames = ["GPU settings"]
                            }
                        ]
                    },

                    // Sound
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Sound",
                        Command = "ms-settings:sound",
                        Glyph = "\ue995",

                        AlternativeNames = ["Audio", "Volume", "Microphone"],

                        Settings = [
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Sound devices",
                                Command = "ms-settings:sound-devices",

                                AlternativeNames = ["Audio devices", "Microphone devices"]
                            },
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Volume mixer",
                                Command = "ms-settings:apps-volume",

                                AlternativeNames = ["Audio mixer"]
                            },

                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Sound",
                                Command = "control.exe /name Microsoft.Sound",

                                AlternativeNames = ["Audio", "Volume", "Microphone"],
                            }
                        ]
                    },

                    // Notifications
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Notifications",
                        Command = "ms-settings:notifications",
                        Glyph = "\uf2a3"
                    },

                    // Energy & battery
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Energy & battery",
                        Command = "ms-settings:batterysaver",
                        Glyph = "\ue7e8",

                        AlternativeNames = [
                            "Battery", "Battery usage", "Battery saver",
                            "Energy", "Energy plans", "Energy saver",
                            "Power", "Power plans", "Power saving",
                            "Power buttons", "Sleep", "Hibernate", "Hibernation"
                        ],

                        Settings = [
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Power Options",
                                Command = "control.exe /name Microsoft.PowerOptions",

                                AlternativeNames = [
                                    "Battery", "Battery options",
                                    "Energy", "Energy plans", "Energy options",
                                    "Power", "Power plans", "Power options",
                                    "Power buttons", "Sleep", "Hibernate", "Hibernation",

                                    "Fast startup", "Fast boot", "Hybrid boot"
                                ]
                            }
                        ]
                    },

                    // Storage
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Storage",
                        Command = "ms-settings:storagesense",
                        Glyph = "\ueda2",

                        AlternativeNames = ["Disks", "Drives"],
                        Settings = [
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Storage Sense",
                                Command = "ms-settings:storagepolicies",
                            },
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Disk & volumes",
                                Command = "ms-settings:disksandvolumes",
                            }
                        ]
                    },

                    // Nearby sharing
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Nearby sharing",
                        Command = "ms-settings:crossdevice",

                        AlternativeNames = ["Shared experiences"]
                    },

                    // Multitasking
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Multitasking",
                        Command = "ms-settings:multitasking",
                        Glyph = "\ue7c4",
                    },

                    // For developers
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "For developers",
                        Command = "ms-settings:developers",
                    },

                    // Activation
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Activation",
                        Command = "ms-settings:activation",
                        Glyph = "\ue930"
                    },

                    // Troubleshoot
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Troubleshoot",
                        Command = "ms-settings:troubleshoot",
                        Glyph = "\ue90f"
                    },

                    // Recovery
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Recovery",
                        Command = "ms-settings:recovery",
                    },

                    // Projecting to this PC
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Projecting to this PC",
                        Command = "ms-settings:projecting",
                        Glyph = "\uebc6"
                    },

                    // Remote Desktop
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Remote Desktop",
                        Command = "ms-settings:remotedesktop",
                        Glyph = "\ue8af"
                    },

                    // Clipboard
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Clipboard",
                        Command = "ms-settings:clipboard",
                        Glyph = "\ue77f"
                    },

                    // System components
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "System components",
                        Command = "ms-settings:systemcomponents",
                    },

                    // AI components
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "AI components",
                        Command = "ms-settings:aicomponents",
                    },

                    // Optional features
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Optional features",
                        Command = "ms-settings:optionalfeatures",
                        Glyph = "\ue71d"
                    },

                    // About
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "About",
                        Command = "ms-settings:about",
                        Glyph = "\ue946",

                        Settings = [
                            new SettingsPage() {
                                Type = SettingType.System32Exe,
                                Name = "Advanced system properties",
                                Command = "SystemPropertiesAdvanced.exe",
                                AlternativeNames = [
                                    "SystemPropertiesAdvanced.exe",
                                    "sysdm.cpl",

                                    "Startup and Recovery",
                                    "Computer Name", "Domain", "Workgroup",
                                    "System Protection", "Restore", "Recovery", "Backup",
                                    "Remote", "Remote Desktop", "Remote Assistance", "Remote Access",
                                ],

                                Settings = [
                                    new() {
                                        Type = SettingType.System32Exe,
                                        Name = "Edit user profiles",
                                        Command = "rundll32 sysdm.cpl,EditUserProfiles"
                                    },
                                    new() {
                                        Type = SettingType.System32Exe,
                                        Name = "Performance & Visual Effects",
                                        Command = "SystemPropertiesPerformance.exe",

                                        AlternativeNames = [
                                            "SystemPropertiesPerformance.exe",
                                            "Performance", "Visual Effects", "Virtual Memory",
                                        ]
                                    },
                                    new() {
                                        Type = SettingType.System32Exe,
                                        Name = "Data Execution Prevention (DEP)",
                                        Command = "SystemPropertiesDataExecutionPrevention.exe",

                                        AlternativeNames = ["SystemPropertiesDataExecutionPrevention.exe"]
                                    },
                                    new() {
                                        Type = SettingType.System32Exe,
                                        Name = "Environment Variables",
                                        Command = "rundll32 sysdm.cpl,EditEnvironmentVariables",
                                        AlternativeNames = [
                                            "Environment Variables", "System variables", "Env Vars"
                                        ]
                                    }
                                ]
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "System information",
                                Command = "msinfo32.exe",
                                AlternativeNames = ["msinfo32.exe"]
                            }
                        ]
                    }
                ]
            },

            // Bluetooth & devices
            new SettingsPage() {
                Type = SettingType.SettingsApp,
                Name = "Bluetooth & devices",
                Command = "ms-settings:devices",
                Glyph = "\ue702",

                Settings = [
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Connected devices",
                        Command = "ms-settings:connecteddevices",

                        Settings = [
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Send or receive files via Bluetooth",
                                Command = "fsquirt.exe",

                                AlternativeNames = ["fsquirt.exe"]
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "More Bluetooth settings",
                                Command = "rundll32.exe shell32.dll,Control_RunDLL bthprops.cpl,,1",

                                AlternativeNames = ["bthprops.cpl"]
                            }
                        ]
                    },

                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Printers & scanners",
                        Command = "ms-settings:printers",
                        Glyph = "\ue749",

                        Settings = [
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Print Management",
                                Command = "printmanagement.msc",

                                AlternativeNames = ["printmanagement.msc", "Printer Spooler"]
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Print Server Properties",
                                Command = "printui.exe /s",

                                AlternativeNames = ["printui.exe", "printui.dll"]
                            }
                        ]
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Phones",
                        Command = "ms-settings:mobile-devices",

                        AlternativeNames = ["Mobile devices"]
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Cameras",
                        Command = "ms-settings:camera",
                    },
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Mouse",
                        Command = "ms-settings:mousetouchpad",
                        Glyph = "\ue962",

                        Settings = [
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Mouse Properties",
                                Command = "control.exe /name Microsoft.Mouse",
                            }
                        ]
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Keyboard",
                        Command = "ms-settings:devices-keyboard",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Touchpad",
                        Command = "ms-settings:devices-touchpad",
                        Glyph = "\uefa5"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "AutoPlay",
                        Command = "ms-settings:autoplay",
                        Glyph = "\uec57"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "USB",
                        Command = "ms-settings:usb",
                        Glyph = "\ue88e"
                    },
                ]
            },

            // Network and internet
            new SettingsPage() {
                Type = SettingType.SettingsApp,
                Name = "Network & internet",
                Command = "ms-settings:network-status",
                Glyph = "\uec27",
                Settings = [
                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "WiFi",
                        Command = "ms-settings:network-wifi",
                        Glyph = "\ue701",

                        Settings = [
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Manage known WiFi networks",
                                Command = "ms-settings:network-wifisettings",
                            }
                        ],
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Ethernet",
                        Command = "ms-settings:network-ethernet",
                        Glyph = "\ue839"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "VPN",
                        Command = "ms-settings:network-vpn",
                        Glyph = "\ue705"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Mobile hotspot",
                        Command = "ms-settings:network-mobilehotspot",
                        Glyph = "\ue88a"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Airplane mode",
                        Command = "ms-settings:network-airplanemode",
                        Glyph = "\ue709"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Proxy",
                        Command = "ms-settings:network-proxy",
                        Glyph = "\ue968"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Dial-up",
                        Command = "ms-settings:network-dialup",
                        Glyph = "\ue83c"
                    },

                    new SettingsPage() {
                        Type = SettingType.SettingsApp,
                        Name = "Advanced network settings",
                        Command = "ms-settings:network-advancedsettings",

                        Settings = [
                            new() {
                                Type = SettingType.SettingsApp,
                                Name = "Advanced network sharing settings",
                                Command = "ms-settings:network-advancedsharing"
                            },

                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Internet options",
                                Command = "inetcpl.cpl",

                                AlternativeNames = ["inetcpl.cpl"]
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Network and sharing center",
                                Command = "control.exe /name Microsoft.NetworkAndSharingCenter",
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Network connections",
                                Command = "ncpa.cpl",

                                AlternativeNames = ["ncpa.cpl"]
                            },
                            new() {
                                Type = SettingType.System32Exe,
                                Name = "Offline files",
                                Command = "control.exe /name Microsoft.OfflineFiles",
                            }
                        ]
                    },
                ]
            },

            // Personalization
            new SettingsPage() {
                Type = SettingType.SettingsApp,
                Name = "Personalization",
                Command = "ms-settings:personalization",

                Settings = [
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Background",
                        Command = "ms-settings:personalization-background",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Colors",
                        Command = "ms-settings:personalization-colors",
                        Glyph = "\ue790"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Themes",
                        Command = "ms-settings:themes",
                        Glyph = "\ue771"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Dynamic lighting",
                        Command = "ms-settings:personalization-lighting",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Lock screen",
                        Command = "ms-settings:lockscreen",
                        Glyph = "\uee3f"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Text input",
                        Command = "ms-settings:personalization-textinput",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Start menu",
                        Command = "ms-settings:personalization-start",
                        Glyph = "\ue8fc"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Taskbar",
                        Command = "ms-settings:taskbar",
                        Glyph = "\ue90e"
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Fonts",
                        Command = "ms-settings:fonts",
                        Glyph = "\ue8d2"
                    },
                ]
            },

            // Windows Update
            new SettingsPage() {
                Type = SettingType.SettingsApp,
                Name = "Windows Update",
                Command = "ms-settings:windowsupdate",
                Glyph = "\ue895",

                Settings = [
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Update history",
                        Command = "ms-settings:windowsupdate-history",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Optional updates",
                        Command = "ms-settings:windowsupdate-optionalupdates",
                    },
                    new() {
                        Type = SettingType.SettingsApp,
                        Name = "Windows Update advanced options",
                        Command = "ms-settings:windowsupdate-options",
                    },
                ]
            }
        ]
    };
}
