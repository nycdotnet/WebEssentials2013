//#region Imports
var coffeescript = require("coffee-script"),
    path = require("path"),
    fs = require("fs"),
    xRegex = require("xregexp").XRegExp;
//#endregion

//#region Handler
var handleCoffeeScript = function (writer, params) {
    var options = {
        filename: params.sourceFileName,
        bare: params.bare != null,
        sourceMap: true,
        sourceRoot: "",
        sourceFiles: [path.relative(path.dirname(params.targetFileName), params.sourceFileName).replace(/\\/g, '/')],
    };

    fs.readFile(params.sourceFileName, 'utf8', function (err, data) {
        if (err) {
            writer.write(JSON.stringify({
                Success: false,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                MapFileName: params.mapFileName,
                Remarks: "CoffeeScript: Error reading input file.",
                Details: err,
                Errors: [{
                    Message: "CoffeeScript: " + err,
                    FileName: params.sourceFileName
                }]
            }));
            writer.end();
            return;
        }

        try {
            compiled = coffeescript.compile(data, options);

            var map = JSON.parse(compiled.v3SourceMap);
            map.file = path.basename(params.targetFileName);
            delete map.sourceRoot;

            var js = compiled.js;
            if (params.sourceMapURL != undefined)
                js = "" + js + "\n//# sourceMappingURL=" + path.basename(params.targetFileName) + ".map\n";

            writer.write(JSON.stringify({
                Success: true,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                MapFileName: params.mapFileName,
                Remarks: "Successful!",
                Content: js,
                Map: JSON.stringify(map)
            }));
            writer.end();
        } catch (error) {
            var regex = xRegex.exec(error, xRegex(".*:.\\d*:.\\d*: error: (?<fullMessage>(?<message>.*)(\\n*.*)*)", 'gi'));
            writer.write(JSON.stringify({
                Success: false,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                MapFileName: params.mapFileName,
                Remarks: "CoffeeScript: An error has occured while processing your request.",
                Details: regex.message,
                Errors: [{
                    Line: error.location.first_line,
                    Column: error.location.first_column,
                    Message: "CoffeeScript: " + regex.message,
                    FileName: error.filename,
                    FullMessage: "CoffeeScript: " + regex.fullMessage
                }]
            }));
            writer.end();
        }
    });
};
//#endregion

//#region Exports
module.exports = handleCoffeeScript;
//#endregion
