// For each argument substring, lists defined strings containing it and the functions referencing them.
// Args: <out dir> <substring> [substring...]   Output: <out>/stringrefs.txt (appended)
// @category SimsRE

import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;

public class StringRefs extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter w = new PrintWriter(new FileWriter(new File(args[0], "stringrefs.txt"), true))) {
            for (int i = 1; i < args.length; i++) {
                String needle = args[i];
                w.println("== " + needle);
                DataIterator it = currentProgram.getListing().getDefinedData(true);
                while (it.hasNext() && !monitor.isCancelled()) {
                    Data d = it.next();
                    if (!d.hasStringValue()) continue;
                    String v = String.valueOf(d.getValue());
                    if (!v.contains(needle)) continue;
                    w.printf("  %s \"%s\"%n", d.getAddress(), v.replace("\n", "\\n"));
                    for (Reference r : getReferencesTo(d.getAddress())) {
                        Function f = getFunctionContaining(r.getFromAddress());
                        w.printf("     <- %s in %s%n", r.getFromAddress(), f == null ? "?" : f.getName() + "@" + f.getEntryPoint());
                    }
                }
            }
        }
    }
}
