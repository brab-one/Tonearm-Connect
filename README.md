# Tonearm Connect

A [Lidarr](https://lidarr.audio) plugin that lets the [Tonearm](https://github.com/brab-one/Tonearm-PhoneApp) phone
app and the [Tonearm desktop app](https://github.com/brab-one/Tonearm-Desktop) see and control each other:
the phone becomes a remote for the desktop player (play/pause, skip, seek, volume, shuffle, repeat, queue),
playback moves between them with **Play this phone's music there** / **Continue on this phone**, and likes of
YouTube Music songs you don't have yet show up on both.

The short install and setup guide for all parts is in [Tonearm](https://github.com/brab-one/Tonearm).

Both apps already reach Lidarr (for requests and Brainarr's picks), often through the same mTLS reverse
proxy as the music server, so Lidarr relays for them and nothing new has to be exposed.

## Install

Needs Lidarr 3.x with plugin support (the same as [Brainarr](https://github.com/RicherTunes/Brainarr)).

1. In Lidarr open **System → Plugins**.
2. Enter `https://github.com/brab-one/Tonearm-Connect` and install.
3. Restart Lidarr. The plugin is listed under System → Plugins, which also offers its updates.

There is nothing to configure in Lidarr: don't add anything under Settings → Connect. In the apps, connect
Lidarr (address and API key) and open **Devices** on the phone.

Manual install: unzip `Tonearm-Connect-v<version>.net8.0.zip` from the
[releases](https://github.com/brab-one/Tonearm-Connect/releases) into
`<Lidarr's config folder>/plugins/brab-one/Tonearm-Connect/` (in Docker, the volume mounted at `/config`)
and restart Lidarr.

## How it works

The plugin is a Lidarr notification provider whose provider action relays messages; it adds no routes of
its own and answers only with Lidarr's API key. The apps call

```
POST /api/v1/notification/action/tonearm?op=<op>&device=<id>…
X-Api-Key: <Lidarr API key>
{"implementation": "TonearmConnect", "configContract": "TonearmConnectSettings",
 "fields": [{"name": "payload", "value": "<JSON>"}]}
```

| op | Does |
| --- | --- |
| `hello` | Plugin and protocol version |
| `publish` | Stores a device's state (the payload: name, kind, what it plays, queue, volume…) |
| `devices` | All devices, with `online` (seen in the last 45 s) |
| `send` | Queues a command (the payload) for `target` |
| `poll` | Commands for `device` after `after`, waiting up to `wait` seconds (max 25) for one |
| `forget` | Removes a device (sent when an app quits) |
| `get` | A shared document by `key` (e.g. `pending-likes`): its `value` and `version` |
| `put` | Saves the payload under `key` if the stored version is still `ifVersion`; otherwise answers `conflict` with what's stored |

Devices and commands live in memory. The shared documents (likes of YouTube Music songs that aren't in the
library yet, so the phone and the desktop show the same ones) are saved in
`<Lidarr's config folder>/tonearm-connect/store.json`.

Everything is kept in memory: after a Lidarr restart the apps simply announce themselves again.

## Build

```bash
./build.sh
```

Needs the .NET 8 SDK. It fetches a Lidarr release to compile against (nothing of it is shipped) and writes
`dist/Tonearm-Connect-v<version>.net8.0.zip`. Lidarr's installer picks the release asset whose name contains
`net8.0.zip`.

## TODO: 
Move tonearm connect to navidrome as plugin
