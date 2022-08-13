# InfiniteModuleReader
Fork of InfiniteModuleReader for dumping tag ids. Reads all modules and dumps tag ids (in both int and big-endian hex formats) to text files.

You can use grep to get all tag ids of a certain type like this:

```
grep deploy -re '\.weapon$' > weapon_ids.txt
```

Derived from:

- https://github.com/Gravemind2401/Adjutant
- https://github.com/MontagueM/HaloInfiniteModuleUnpacker
- https://github.com/ElDewrito/AusarDocs

Requires https://github.com/Krevil/OodleSharp which is forked from https://github.com/Crauzer/OodleSharp and uses modifications provided by Zeddikins

As well as support from Matthew, Exhibit, Sopitive, Zeddikins and other members of the Halo Modding community.
