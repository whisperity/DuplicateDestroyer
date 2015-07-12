DuplicateDestroyer
==================
DuplicateDestroyer removes duplicate files from a directory tree

How to use?
-----------
* Put the executable into the top folder of the tree you want to check
* Start the application. (Either by double click or from the command line: `DuplicateDestroyer.exe`)
* You will be asked which files to keep or delete from a set of exact duplicates
* Alternatively, you might specify `-o` or `-n` to auto-schedule the oldest or newest files
* The files are deleted by the program

Implications/Limitations
------------------------
DuplicateDestroyer checks for duplicates by reading every file in the tree and calculating an [MD5](http://en.wikipedia.org/wiki/MD5) hash on it.

You should **never** run data removers without making an appropriate **backup** first.

DuplicateDestroyer does **not** put your files into the _Recycle Bin_. The files are **permanently deleted** on impact.

Parameters
----------
    DuplicateDestroyer.exe
    
    -h    Show this help text
    -v    Verbose mode
    -d    Dry run/discovery - Only check for duplicates, but don't actually remove them
    -o    Automatically keep the OLDEST of the files
    -n    Automatically keep the NEWEST of the files

Exit codes
----------
    0   Program terminated normally
    1   Program failure, the internal data got corrupted
    2   One or more files' removal failed
    3   Configuration error

License
-------
> Tiny Droplet Licence
> Revision 1, Last edited 23/12/12.
> 
> You are allowed to dual licence (use multiple licences such as GNU General Licence or Creative Commons as long as they are compatible) all material owned by Cloud Chiller under this licence as long as Cloud Chiller is credited properly.
> Cloud Chiller retains all Copyright and you are given permission to use, edit, redistrobute, sell, dual licence everything released under this licence. Cloud Chiller cannot revoke these permissions unless you (the user) break this licence.
