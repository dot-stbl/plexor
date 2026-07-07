import type { GetQueryResponse } from "../types/Get.ts";
import { http } from "msw";

 export function getHandler(data?: GetQueryResponse) {
    return http.get("*/", function handler(info) {
        return new Response(JSON.stringify(data), {
            headers: {
                "Content-Type": "application/json",
            },
        });
    });
}