local ui, uiu, uie = require("ui").quick()
local fs = require("fs")
local utils = require("utils")
local threader = require("threader")
local scener = require("scener")
local config = require("config")
local sharp = require("sharp")
local native = require("native")
local alert = require("alert")
local notify = require("notify")
local modinstaller = require("modinstaller")

function love.filedropped(file)
    threader.routine(function()
        if #alert.root.children > 0 or scener.locked then
            return
        end

        local install = config.installs[config.install]
        if not install then
            alert({
                body = [[
Your Celeste installation list is still empty.
Do you want to go to the Celeste installation manager?
]],
                buttons = {
                    {
                        "Yes",
                        function(container)
                            scener.push("installmanager")
                            container:close("OK")
                        end
                    },
                    { "No" }
                }
            })
            return
        end

        file = file:getFilename()
        if not fs.isFile(file) then
            print("user drag-n-dropped pathless file?")
            notify("Olympus can't handle that file - does it exist?")
            return
        end

        local filetype = sharp.scanDragAndDrop(file):result()

        if filetype == "mod" then
            modinstaller.install("file://" .. file)

        elseif filetype == "everest" then
            local installer = scener.push("installer")
            installer.sharpTask("installEverest", install.path, "file://" .. file):calls(function(task, last)
                if not last then
                    return
                end

                installer.update("Everest successfully installed", 1, "done")
                installer.done({
                    {
                        "Launch",
                        function()
                            utils.launch(install.entry.path)
                            scener.pop()
                        end
                    },
                    {
                        "OK",
                        function()
                            scener.pop()
                        end
                    }
                })
            end)

        else
            print("user drag-n-dropped file of unknown type", file)
            notify("Olympus can't handle that file.")
        end
    end)
end