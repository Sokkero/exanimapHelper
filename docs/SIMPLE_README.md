# ExanimapHelper

### Welcome
I applaud and welcome any person with non-technical backgrounds who braves themselves onto github to look at projects.<br>

I wrote this readme document for you in hopes to be able to clarify all your questions. <br>Should any uncertainties still concern you, feel free to open an issue right here!
> To open an issue: navigate to `Issues` -> `New Issue`

## What you'll need
To be able to use this tool, you will require to also use the application `Cheat Engine`, which can be found [here](https://www.cheatengine.org/).

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
> To approve, you can click on "More details" and then "Run anyway" on the Windows Defender pop-up. But:
>
> <i><u>Its always better to <b>not</b> download a software youre unsure of!</u></i>

## Getting started
Before you can start using this tool to record all your movement, you will need to find the exact memory addresses of both the X and Y axis of your character. 

Sounds complicated? [I've got you covered.](CHEAT_ENGINE_INSTRUCTIONS.md)

Once you have located the memory addresses, open this tool and paste them into their corresponding fields.

Now tab back into the game and hit F8. If all is set up correctly, the tool will now record your movement in-game, until you hit F8 again. On the lower left corner, you should be able to see the recorded values. On the right hand side, the program will draw a walk path from those points, which you can then later on export as a png.

Thats pretty much it, have fun recording!

> ⚠️ One thing to remember: the memory addresses change every time you restart Exanima. If you close and reopen the game, you'll need to find the addresses again before recording.