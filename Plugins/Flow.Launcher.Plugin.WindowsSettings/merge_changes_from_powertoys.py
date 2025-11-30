# A python script that merges new changes from PowerToys' WindowsSettings.json into our WindowsSettings.json
# https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.WindowsSettings/WindowsSettings.json

import argparse
import json
import sys
from dataclasses import dataclass

VALID_TYPES = {'AppSettingsApp', 'AppControlPanel', 'AppMMC'}

@dataclass
class PowertoysSetting:
    Name: str = None
    Type: str = None
    Command: str = None
    Areas: list[str] | None = None
    Note: str | None = None
    AltNames: list[str] | None = None
    AppHomepageScore: int | None = None
    DeprecatedInBuild: int | None = None
    IntroducedInBuild: int | None = None

    def __post_init__(self):
        obligatory_fields = ['Name', 'Type', 'Command']
        for field in obligatory_fields:
            if getattr(self, field) is None:
                abort(f"Missing obligatory field '{field}' in PowertoysSetting {self}")

        if self.Type not in VALID_TYPES:
            abort(f"Unknown Type '{self.Type}' in PowertoysSetting {self}")

    def __eq__(self, other):
        if not isinstance(other, PowertoysSetting) and not isinstance(other, LocalSetting):
            return NotImplemented

        return self.Name == other.Name and self.Command == other.Command

    def __hash__(self):
        return hash((self.Name, self.Command))

@dataclass
class LocalSetting:
    Name: str = None
    Type: str = None
    Command: str = None
    Areas: list[str] | None = None
    Note: str | None = None
    AltNames: list[str] | None = None
    DeprecatedInBuild: int | None = None
    IntroducedInBuild: int | None = None
    IconGlyph: dict | None = None

    def __post_init__(self):
        obligatory_fields = ['Name', 'Type', 'Command']
        for field in obligatory_fields:
            if getattr(self, field) is None:
                abort(f"Missing obligatory field '{field}' in LocalSetting {self}")

        if self.Type not in VALID_TYPES:
            abort(f"Unknown Type '{self.Type}' in LocalSetting {self}")

    def __eq__(self, other):
        if not isinstance(other, PowertoysSetting) and not isinstance(other, LocalSetting):
            return NotImplemented

        return self.Name == other.Name and self.Command == other.Command

    def __hash__(self):
        return hash((self.Name, self.Command))

def abort(msg: str):
    print(f"Error: {msg}", file=sys.stderr)
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(
        description='Merge new changes from PowerToys\' WindowsSettings.json into our WindowsSettings.json'
    )
    parser.add_argument('powertoys_json', type=argparse.FileType('r', encoding='utf-8'))
    parser.add_argument('local_json', type=argparse.FileType('r', encoding='utf-8'))
    args = parser.parse_args()

    powertoys_settings_list: list[dict] = json.load(args.powertoys_json).get('Settings', [])
    local_settings_list: list[dict] = json.load(args.local_json)
   
    print('Validating PowerToys JSON...')
    powertoys_settings = {PowertoysSetting(**setting) for setting in powertoys_settings_list}
    duplicates = len(powertoys_settings_list) - len(powertoys_settings)
    if duplicates > 0:
        print(f'Warning: {duplicates} duplicate setting(s) found')

    print('Validating Local JSON...')
    local_settings = {LocalSetting(**setting) for setting in local_settings_list}
    duplicates = len(local_settings_list) - len(local_settings)
    if duplicates > 0:
        abort(f'{duplicates} duplicate setting(s) found')

    added_settings = powertoys_settings - local_settings
    removed_settings = local_settings - powertoys_settings
    renamed_settings = {s1: s2 for s1 in added_settings for s2 in removed_settings if s1.Command == s2.Command or s1.Name == s2.Name}

    for setting in added_settings:
            if setting not in renamed_settings:
                print(f'[+] New setting: {setting.Name} ({setting.Command})')
                local_settings.add(LocalSetting(
                    Name=setting.Name,
                    Type=setting.Type,
                    Command=setting.Command,
                    Areas=setting.Areas,
                    Note=setting.Note,
                    AltNames=setting.AltNames,
                    DeprecatedInBuild=setting.DeprecatedInBuild,
                    IntroducedInBuild=setting.IntroducedInBuild,
                    IconGlyph=None
                ))
    for setting in removed_settings:
            if setting not in renamed_settings.values():
                print(f'[-] Removed setting: {setting.Name} ({setting.Command})')
                local_settings.remove(setting)
    for new, old in renamed_settings.items():
            print(f'[~] Renamed/Changed setting: {old.Name} ({old.Command}) -> {new.Name} ({new.Command})')
            local_settings.remove(old)
            old.Name = new.Name
            old.Type = new.Type
            old.Command = new.Command
            old.Areas = new.Areas
            old.Note = new.Note
            old.AltNames = new.AltNames
            old.DeprecatedInBuild = new.DeprecatedInBuild
            old.IntroducedInBuild = new.IntroducedInBuild
            local_settings.add(old)

    local_settings = sorted(local_settings, key=lambda s: s.Name.lower())
    local_settings = [{K: v for K, v in cfg.__dict__.items() if v is not None} for cfg in local_settings]
    with open(args.local_json.name, 'w', encoding='utf-8') as f:
        json.dump(local_settings, f, indent=4)

    print()
    print('[*] Done')

if __name__ == '__main__':
    main()