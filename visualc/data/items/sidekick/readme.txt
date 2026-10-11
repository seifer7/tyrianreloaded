# (String) ID string used to identify this sidekick [10 chars]
id=atomsplit

# (String) Name shown in game [30 chars]
name=Atom Splitter

# (Int) ID of sprite to display in shop
itemgraphic=115

# (Int) How many "charge" power levels this sidekick can powerup. 
# Each powerlevel will increment the wpnum by 1. 
# This needs to be addressed by adding a field that stores the wpnum ID string for each powerlevel
# The current charge level is also used to change the sprites used (gr0, gr1, gr2, gr3, gr3)
# Only 0-4 is accepted.
pwr=0

# (Int) Cost of item in store
cost=200000

# (Int) Style of sidekick. 0 = normal, 1, 3 = trailing (difference?), 2 = front-mounted, 4 = orbiting
tr=0

# (Int) 0 = Stop animation when charged up. >0 = Repeat animation. Other effects unknown.
option=1

# Not used
opspd=3

# (Int) Total number of animation frames
ani=12

# (Space separated list of Int) ID # of sprite to use for each frame of sidekick animation (20 frames)
# See "pwr" above for explaination on gr1, gr2, gr3, & gr4.
gr0=87 87 87 106 106 106 125 125 125 144 144 144 0 0 0 0 0 0 0 0
gr1=0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
gr2=0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
gr3=0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
gr4=0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0

# (String) Front/Rear Weapon ID to use as reference for power drain (usually 16) [10 chars]
wport=16

# (String) ID string of shot to use when firing [10 chars]
wpnum=atomsplit

# (Int) Ammo of sidekick. 0 for unlimited
ammo=0

# Not used
stop=1

# (Int) ID of sprite to use for in-game HUD
icongr=6

# (Int) 0 = Both, 1 = Left only, 2 = Right only
position=0

# (Int) 0-100, probability of item to appear in store when "Random" store contents are enabled.
probability=100
