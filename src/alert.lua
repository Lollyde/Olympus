local ui, uiu, uie = require("ui").quick()
local threader = require("threader")

local alert = {}


function alert.init(root)
    alert.root = root
end


function alert.show(data)
    if type(data) == "string" then
        data = {
            body = data
        }
    end

    local container = uie.column({}):with({
        time = 0,
        interactive = 2,
        style = {
            bg = { 0.1, 0.1, 0.1, 0 },
            padding = 0,
            radius = 0
        },
        clip = false,
        cacheable = false
    }):with(uiu.fill)
    alert.root:addChild(container)

    local box = uie.panel({}):with({
        style = {
            padding = 16
        }
    }):hook({
        layoutLateLazy = function(orig, self)
            -- Always reflow this child whenever its parent gets reflowed.
            self:layoutLate()
        end,

        layoutLate = function(orig, self)
            orig(self)
            self.x = math.floor(self.parent.innerWidth * 0.5 - self.width * 0.5)
            self.realX = math.floor(self.parent.width * 0.5 - self.width * 0.5)
            self.y = math.floor(self.parent.innerHeight * 0.5 - self.height * 0.5)
            self.realY = math.floor(self.parent.height * 0.5 - self.height * 0.5)
        end
    })
    local boxBG = box.style.bg
    box.style.bg = { boxBG[1], boxBG[2], boxBG[3], 1 }

    if data.title then
        box:addChild(uie.label(data.title, ui.fontBig))
    end

    if data.body then
        if type(data.body) == "string" then
            box:addChild(uie.label(data.body))
        else
            box:addChild(data.body)
        end
    end

    container:addChild(box)


    function container.close(reason)
        if container.closing then
            return
        end
        container.closing = true
        container.time = 0
        if data.cb then
            data.cb(reason)
        end
    end


    container:hook({
        update = function(orig, self, dt)
            orig(self, dt)

            local time = container.time
            if container.closing then
                time = time + dt
                if time >= 0.3 then
                    self:removeSelf()
                    return
                end
                container.style.bg[4] = math.min(0.6, (0.2 - time) * 6)

            else
                time = time + dt
                if time > 1 then
                    time = 1
                end
                container.style.bg[4] = math.min(0.6, time * 6)
            end

            container.time = time
        end,

        onClick = function(orig, self, x, y, button)
            orig(self, x, y, button)
            if not data.force then
                container.close("bypass")
            end
        end
    })

    return container
end


local mtAlert = {
    __call = function(self, ...)
        self.show(...)
    end
}

setmetatable(alert, mtAlert)

return alert
