# Cheat Engine instructions

## Introduction
This document will help you find the x and y data variables in exanima.<br>
If you have never used cheat engine before, I recommend you following the tutorial they offer you when starting Cheat Engine for the first time. If you do not wnat to do that, thats fine, the following steos should still be able to walk you through everything

## Getting started
Start exanima and load a savegame. Ensure you have a compass with you. Now navigate into a room where you have a lot of space and are safe.

Now open cheat engine and press the button on the top left. Now select exanima in the list of applications.

If you did a scan before, click `New Scan` at the top right.

Now to start the search, go the right-hand side of the window and there select:
- Scan Type: `Unknown initial value`
- Value type: `float`

Then click `First scan`

2x Now walk South-East and make the next scan, this time of type `Increased value`

2x Now walk North-West and make the next scan, this time of type `Decreased value`

Now ensure you have not moved your character since the last scan (if you did or are unsure, simply move and then select `changed value` and hit scan). 
Now stand still and select `unchanged value` and tick `Repeat`. Now let it run through for a few seconds. Whilst, rotate the camera once.
 
## Finding the X-Axis

- Continue here right after getting started
- Walk North-East and scan with type `Increased Value`
- Walk South-West and scan with type `Decreased Value`
- Repeat these steps a couple of time until <20 values are left
- Most of these should contain very similar values (difference is less than 5.f)
- Double click it so it appears at the bottom
- You have found the X-Axis, now repeat for the Y-Axis!

## Finding the Y-Axis

- Continue here right after getting started
- Walk South-East and scan with type `Increased Value`
- Walk North-West and scan with type `Decreased Value`
- Repeat these steps a couple of time until <20 values are left
- Most of these should contain very similar values (difference is less than 5.f)
- Double click it so it appears at the bottom
- You have found the Y-Axis, now repeat for the Y-Axis!

Now simply right click on the address and copy it over into exanimapHelper.

Done