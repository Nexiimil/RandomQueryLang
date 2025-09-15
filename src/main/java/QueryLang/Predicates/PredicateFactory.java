package QueryLang.Predicates;

import QueryLang.Thing;

public class PredicateFactory {
    public static IPredicate ProcessQuery(String queryString) {
        // Dummy implementation for demonstration purposes
        return null; // Replace with actual logic to parse and create appropriate IPredicate    
    }

    public static Thing CreateThing(String thingString) {

        thingString = SanitizeThingString(thingString);

        if (ValidThingString(thingString)) {
            String[] parts = thingString.split(";");

            if(parts.length == 1) {
                return new Thing(parts[0].trim(), 0, 0);
            }

            if(parts.length == 2) {
                return new Thing(parts[0].trim(), Integer.parseInt(parts[1].trim()), 0);
            }

            if(parts.length == 3) {
                return new Thing(parts[0].trim(), Integer.parseInt(parts[1].trim()), Float.parseFloat(parts[2].trim()));
            }
        }

        throw new IllegalArgumentException("Invalid Thing string: " + thingString);
    }

    private static String SanitizeThingString(String thingString) {
        if (thingString == null) {
            return "";
        }

        thingString = thingString.trim();

        thingString = thingString.replace("Thing(", "").replace(")", "");

        thingString = thingString.replace(" ; ", ";");
        thingString = thingString.replace("; ", ";");
        thingString = thingString.replace(" ;", ";");

        return thingString;
    }

    private static boolean ValidThingString(String thingString) {
        if (thingString == null || thingString.isEmpty()) {
            return false;
        }

        String[] parts = thingString.split(";");

        if (parts.length > 3 || parts.length < 1) {
            return false;
        }

        try {
            Integer.parseInt(parts[1].trim());
            Float.parseFloat(parts[2].trim());
        } catch (NumberFormatException e) {
            return false;
        }
        return true;
    }
}
