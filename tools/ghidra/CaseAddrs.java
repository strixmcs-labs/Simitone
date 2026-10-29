// Prints the entry address of every switch case (caseD_N labels) inside one function.
// Args: <function addr>
// @category SimsRE

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;

import java.util.Map;
import java.util.TreeMap;

public class CaseAddrs extends GhidraScript {
    @Override
    protected void run() throws Exception {
        Function f = getFunctionAt(toAddr(getScriptArgs()[0]));
        Map<String, Address> rows = new TreeMap<>();
        SymbolIterator it = currentProgram.getSymbolTable().getSymbols(f.getBody(), ghidra.program.model.symbol.SymbolType.LABEL, true);
        while (it.hasNext()) {
            Symbol s = it.next();
            if (s.getName().startsWith("caseD_")) {
                rows.put(s.getParentNamespace().getName() + " " + s.getName(), s.getAddress());
            }
        }
        rows.forEach((k, v) -> println(k + " " + v));
    }
}
