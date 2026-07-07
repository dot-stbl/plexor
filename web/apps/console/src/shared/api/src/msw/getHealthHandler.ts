import type { GetHealthQueryResponse } from "../types/GetHealth.ts";
import { http } from "msw";

 export function getHealthHandler(data?: GetHealthQueryResponse) {
    return http.get("*/health", function handler(info) {
        return new Response(JSON.stringify(data), {
            headers: {
                "Content-Type": "application/json",
            },
        });
    });
}