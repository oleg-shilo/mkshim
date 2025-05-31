# Release v1.4.0.0

- Implemented new switches (see https://github.com/oleg-shilo/mkshim/wiki#use-cases)
    `--console|-c`
    `--console-hidden|-ch`
    `--win|-w`
    `--wait-pause 
    Obsoleted `--no-console|-nc`. Use `--win` instead.
    Obsoleted `--wait-onexit` Use `--wait-pause` instead.
- Added handling (reporting) of obsolete CLI arguments