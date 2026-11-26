# SpareKey

My attempt at a cross-platform password manager for Windows and Linux.

One master password, plus 24 recovery words as a spare key if you forget it.
Everything stays on your PC. No cloud, no account.

Note: Test it before you trust it with your real passwords.


## Tech stuff

- .NET 10 + C#, Avalonia UI for the interface
- AES-256-GCM for the encryption, because it also notices if someone messed with the file
- Argon2id to turn your password into a key. Its slow but makes guessing expensive.
- BIP39 word list for the 24 recovery words


## How it works (short version)

When you create a vault, the app makes a random master key. You never see it.
That key gets locked twice inside the file in two "boxes"

- box 1 unlocks with your password
- box 2 unlocks with the 24 words

So it's the same masterkey locked with two different ways. With 24 words and your passwords. Forgot the password? Use the words and set a new one.
When you change the password, the masterkey remains the same. Just locked in the box 1 with different password. Therefore the the 24 words still working for a new recovery.

The file also seals its own header, so if anything in it gets changed or damaged,
it refuses to open instead of giving you garbage.


## If your PC breaks

You need three things:

1. your vault file (or the .bak backup next to it)
2. your 24 words, on paper
3. the app (grab it again from Releases)

Open the file, type your password or click "Forgot password?"to type the words and set a new passsword, done.
Test these steps before you store your valuable passwords. Seriously.


## Download

Grab a build from the Releases page.

- **Windows:** unzip and run SpareKey.exe. (It's not code-signed)
- **Linux:** unzip, then `chmod +x SpareKey` and `./SpareKey`.
  
The app icon may not show in Linux yet but I am planning to fix this.

The "self" builds have .NET inside, nothing to install. The small ones need the .NET 10 runtime.


## Build it yourself

Open SpareKey.slnx in Visual Studio (for .Net 10, version 2026 is required)
Or double-click one of the .bat scripts in `builders/`, the output lands in `publish/`.


## Known limits

Being honest here:

- Old backups still open with your OLD password.
- Clipboard clears after 30 seconds, but Windows clipboard history (Win+V) and Linux
  clipboard managers can keep their own copy.
- Passwords are .NET strings in memory while the vault is open. They can't be wiped,
  and also the OS can write memory to swap. But the master key itself does get wiped when it locks.
- Personal fun project, it's not security-reviewed.


## License

See LICENSE.