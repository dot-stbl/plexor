import client from "@kubb/plugin-client/client";
import type { RequestConfig } from "@kubb/plugin-client/client";
import type { GetQueryResponse } from "../types/Get.ts";

 /**
 * @link /
 */
export async function get(config: Partial<RequestConfig> = {}) {
    const res = await client<GetQueryResponse, Error, unknown>({ method: "GET", url: `/`, ...config });
    return res.data;
}