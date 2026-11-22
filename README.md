\# SpareKey



My attempt at a cross-platform password manager for windows and linux 



Tech stuff: .NET 10 + C#
Avalonia UI for interface

AES-256-GCM because it can detect if someone messed with the file

Argon2id for the key

BIP39 for the 24 recovery words 




Plan: 
AES-256-GCM encryption, one master password, and 24 recovery words as a spare key if you forget it.



Everything stays on your PC, no cloud. 



If you brake your PC? with the Backup file + 24 rando recovery words and get your passwords back



Just an attempt. Don't trust your passwords yet lol

