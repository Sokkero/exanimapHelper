# ExanimapHelper

### Welcome
I applaud and welcome any person with non-technical backgrounds who braves themselves onto github to look at projects.<br>

I wrote this readme document for you in hopes to be able to clarify all your questions. <br>Should any uncertainties still concern you, feel free to open an issue right here!
> To open an issue: navigate to `Issues` -> `New Issue`

## What you'll need
First, download the latest (stable) version of this tool from the [releases page](https://github.com/Sokkero/exanimapHelper/releases).

In most cases that's all you need: the tool already knows Exanima's default memory locations and tries to read your character's position from them automatically.

Sometimes those defaults don't work (for example after a game update). If that happens, you might also need the application `Cheat Engine`, which can be found [here](https://www.cheatengine.org/), to find the locations yourself — see [Getting started](#getting-started) below.

> <b>Cheat Engine? Whats that?</b>
>
> Cheat Engine is a tool which allows you to hook onto a running application and read out all currently stored data in memory. It only ever hooks onto an application and reads its data if that application has been specifically selected by the user. It is fully open source and trusted by thousands (currently starred by ~18.5k people on github).

> <b>Wont I risk getting banned by VAC/Vanguard/etc?</b>
>
> As long as you don't try to hook cheat engine onto CS2/LoL/Valorant/etc, I can confidently ensure you: no, you won't get banned. Anti-cheat systems only ever care about what's going on with their games and exanima is a singleplayer game without any anti-cheat.<br>
> Now with Vanguard being a kernel level anti-cheat, I do recommend to close cheat-engine when starting a vanguard protected game. I have not heard of any false positives through cheat-engine but have heard that the games might refuse to launch if cheat-engine is currently running (even if idle).

> <b>Windows defender blocked cheatengine/exanimapHelper. Why?</b>
>
> It makes perfect sense that Windows Defender (and/or maybe some anti-virus software) will try to block you from starting these applications.<br>
> These types of software want to read the data in memory of other programs, and to do so they need to register themselves with [administrative intent](../app.manifest), which windows does not like.
>
> Imagine it like this: A random person walks into IKEA and says "Hey, I'll just quickly go into your offices and look into some files, okay?". Of course any IKEA employee is going to stop this person and say "No, you cannot do that". So, it requires the manager of the store (in this case you), to approve of this and say "it's fine, they are allowed to".
>
> To approve, you can click on "More info" and then "Run anyway" on the Windows Defender pop-up. But:
>
> <i><u>Its always better to <b>not</b> download a software youre unsure of!</u></i>

## Getting started
Start Exanima, then open this tool. It comes pre-filled with Exanima's default memory addresses, so usually you can jump straight in: tab back into the game and hit F8. If all is set up correctly, the tool will now record your movement in-game, until you hit F8 again. On the lower left corner, you should be able to see the recorded values. On the right hand side, the program will draw a walk path from those points, which you can then later on export as a png.

If the tool can't read a valid position with the defaults (you'll see no values, or obviously-wrong ones), you'll need to find the addresses of your X and Y axis yourself. Sounds complicated? [I've got you covered.](CHEAT_ENGINE_INSTRUCTIONS.md) Once you've located them, paste them into their corresponding fields and try again.

Thats pretty much it, have fun recording!

> ⚠️ One thing to remember: the built-in defaults keep working across game restarts, so you don't need to do anything special. But if you had to find the addresses yourself with Cheat Engine, only the **green** (persistent) ones survive a restart — pick those, or you'll have to find them again every time you reopen the game.

### Pre-made maps
If youd like to use this application to simply track your movements on an already existing map, you can use the import functionality to import a pre-made map. Some maps ready for import can be found in the `trails/` folder of this project. Simply downlaod it, start the app, select "Import Data" and select the file!
Also, feel free to contibute to the collection by either creating a PR or sharing your data through a new `Issue`!

> <b>I'd like to contribute directly, but what is a "PR"?</b>
>
> A PR (Pull request) is a request you can make, to contribute changes to this project. For this, you will have to [setup git on your machine](https://www.w3schools.com/git/), fork this repository, add the file into your forked repository, push it and [create a pull request](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-pull-requests) here.
>
> Sounds to complicated?<br>I get that, just create a new issue and I'll take care of it for you!

> To open an issue: navigate to `Issues` -> `New Issue`
