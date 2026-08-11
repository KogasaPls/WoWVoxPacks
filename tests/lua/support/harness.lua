-- A test registry small enough to read in one sitting. No dependencies, so the only thing
-- standing between CI and the generated Lua is luajit.

local harness = { cases = {} }

function harness.test(name, fn)
    harness.cases[#harness.cases + 1] = { name = name, fn = fn }
end

local function Describe(value)
    if type(value) == "string" then return string.format("%q", value) end
    return tostring(value)
end

function harness.equal(actual, expected, message)
    if actual ~= expected then
        error(string.format("%s\n  expected: %s\n  actual:   %s",
            message or "not equal", Describe(expected), Describe(actual)), 2)
    end
end

function harness.truthy(value, message)
    if not value then
        error(message or "expected a truthy value", 2)
    end
end

function harness.falsy(value, message)
    if value then
        error(string.format("%s (got %s)", message or "expected a falsy value",
            Describe(value)), 2)
    end
end

function harness.contains(list, value, message)
    for _, item in ipairs(list) do
        if item == value then return end
    end
    error(string.format("%s\n  wanted:  %s\n  in:      {%s}",
        message or "value not found", Describe(value), table.concat(list, ", ")), 2)
end

--- Fails when any entry of `list` contains `pattern` as a plain substring.
function harness.none_matching(list, pattern, message)
    for _, item in ipairs(list) do
        if tostring(item):find(pattern, 1, true) then
            error(string.format("%s\n  unwanted: %s\n  found in: %s",
                message or "unexpected entry", Describe(pattern), Describe(item)), 2)
        end
    end
end

function harness.count(list, message, expected)
    harness.equal(#list, expected, message)
end

function harness.run()
    local failed = 0
    for _, case in ipairs(harness.cases) do
        local ok, err = pcall(case.fn)
        if ok then
            io.write("ok   ", case.name, "\n")
        else
            failed = failed + 1
            io.write("FAIL ", case.name, "\n     ", tostring(err):gsub("\n", "\n     "), "\n")
        end
    end
    io.write(string.format("\n%d passed, %d failed, %d total\n",
        #harness.cases - failed, failed, #harness.cases))
    return failed
end

return harness
