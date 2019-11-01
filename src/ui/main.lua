local ui = {}

ui.hovering = nil
ui.dragging = nil
ui.draggingCounter = 0
ui.focused = nil

function ui.update()
    local root = ui.root
    
    ui.delta = love.timer.getDelta()

    root:update()
    root:layout()

    root:layoutLate()
    root:updateLate()

end


function ui.draw()
    local root = ui.root
    
    root:draw()
end


function ui.interactiveIterate(el, funcid, ...)
    if not el then
        return nil
    end

    local parent = el.parent
    if parent then
        parent = ui.interactiveIterate(parent, funcid, ...)
    end

    if funcid then
        el[funcid](el, ...)
    end
    
    if el.interactive == 0 then
        return parent
    end
    return el
end

function ui.mousemoved(x, y, dx, dy, istouch)
    local ui = ui
    local root = ui.root
    if not root then
        return
    end

    local hoveringPrev = ui.hovering
    local hoveringNext = root:getChildAt(x, y)
    ui.hovering = hoveringNext
    
    if hoveringPrev ~= hoveringNext then
        if hoveringPrev then
            hoveringPrev:onEnter()
        end
        if hoveringNext then
            hoveringNext:onLeave()
        end
    end

    local dragging = ui.dragging
    if dragging then
        dragging:onDrag(x, y, dx, dy)
    end
end

function ui.mousepressed(x, y, button, istouch)
    local ui = ui
    local root = ui.root
    if not root then
        return
    end

    ui.draggingCounter = ui.draggingCounter + 1

    local hovering = root:getChildAt(x, y)
    if hovering then
        if ui.dragging == nil or ui.dragging == hovering then
            local el = ui.interactiveIterate(hovering, "onPress", x, y, button, true)
            ui.dragging = el
            ui.focused = el
        else
            ui.interactiveIterate(hovering, "onPress", x, y, button, false)
        end
    end
end

function ui.mousereleased(x, y, button, istouch)
    local ui = ui
    local root = ui.root
    if not root then
        return
    end

    ui.draggingCounter = ui.draggingCounter - 1

    local dragging = ui.dragging
    if dragging then
        if ui.draggingCounter == 0 then
            ui.dragging = nil
            ui.interactiveIterate(dragging, "onRelease", x, y, button, false)
            if dragging == ui.interactiveIterate(root:getChildAt(x, y)) then
                ui.interactiveIterate(dragging, "onClick", x, y, button)
            end
        else
            ui.interactiveIterate(dragging, "onRelease", x, y, button, true)
        end
    else
        local hovering = root:getChildAt(x, y)
        if hovering then
            ui.interactiveIterate(dragging, "onRelease", x, y, button, false)
        end
    end
end


return ui
