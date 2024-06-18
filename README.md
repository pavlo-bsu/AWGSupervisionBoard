# AWGSupervisionBoard
The application controls `Arbitrary Waveform Generator` M8195A (Keysight &copy;) via `Standard Commands for Programmable Instruments (SCPI)`.

The application allows you to generate a signal at the generator output with specified amplitude and pulse repetition frequency.

Time domain of a signal is loaded from a data file, displayed in a main window (with help of `OxyPlot` library) and then uploaded to generator’s internal memory.

UML sequence (see Figure below) shows simplified interactions between a user, PC and Arbitrary Waveform Generator to start a generation of a signal.

![Figure 1]( https://raw.githubusercontent.com/pavlo-bsu/AWGSupervisionBoard/backmatter/img/umlStartSingalGeneration.png)

## Notes
* `The application is just a small example` of the capabilities of Arbitrary Waveform Generator. `The real application for a Customer` uses extensive capabilities of Arbitrary Waveform Generator such as sequences, launch of signal generation via a trigger input or via application command, etc.
* Keysight library `Ivi.Visa.Interop` is not included in the solution, and can be easily found in Keysight official web site: https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/m8195a-firmware-and-soft-front-panel-including-instrument-driver-2527166.html.