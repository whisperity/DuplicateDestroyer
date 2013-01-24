DuplicateDestroyer
==================

DuplicateDestroyer removes duplicate files from a directory tree.

How to use?
-----------
* Put the executable into the top folder of the tree you want to check.
* _Optional step_ To only check for duplicates (but to **avoid** accidentally removing them), run the program normally.
* Run it from the command-line as `DuplicateDestroyer.exe -ok`. This run will actually delete the files.

Implications/Limitations
------------------------
DuplicateDestroyer checks for duplicates by reading every file in the tree and calculating an [MD5](http://en.wikipedia.org/wiki/MD5) hash on it.
Because this is a costly method, please refrain from running on either _directories containing massive number of files_, or _directories with really big files in it_.

You should **never** run data removers without making an appropriate **backup** first.

DuplicateDestroyer does **not** put your files into the _Recycle Bin_. The files are **permanently deleted** on impact.

Parameters
----------
    DuplicateDestroyer.exe
    
    -h    Show this help text.
    -ok   Safety disable. If omitted, will run in read-only mode.
    -v    Verbose mode
    -o    Automatically schedule the OLDEST file for keeping.
    -n    Automatically schedule the NEWEST file for keeping.

Exit codes
----------
    0   Program terminated normally
    1   Exception happened while counting files or iterating the tree
    2   One or more files failed to be deleted
    3   Configuration error (i.e.: using both -o and -n switches)

License
-------
> Tiny Droplet Licence
> Revision 1, Last edited 23/12/12.
> 
> You are allowed to dual licence (use multiple licences such as GNU General Licence or Creative Commons as long as they are compatible) all material owned by Cloud Chiller under this licence as long as Cloud Chiller is credited properly.
> Cloud Chiller retains all Copyright and you are given permission to use, edit, redistrobute, sell, dual licence everything released under this licence. Cloud Chiller cannot revoke these permissions unless you (the user) break this licence.