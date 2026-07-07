import client from "@kubb/plugin-client/client";
import type { RequestConfig } from "@kubb/plugin-client/client";
import type { QueryKey, QueryObserverOptions, UseQueryResult } from "@tanstack/react-query";
import type { GetQueryResponse } from "../types/Get.ts";
import { queryOptions, useQuery } from "@tanstack/react-query";

 export const getQueryKey = () => [{ url: "/" }] as const;

 export type GetQueryKey = ReturnType<typeof getQueryKey>;

 /**
 * @link /
 */
async function get(config: Partial<RequestConfig> = {}) {
    const res = await client<GetQueryResponse, Error, unknown>({ method: "GET", url: `/`, ...config });
    return res.data;
}

 export function getQueryOptions(config: Partial<RequestConfig> = {}) {
    const queryKey = getQueryKey();
    return queryOptions({
        queryKey,
        queryFn: async ({ signal }) => {
            config.signal = signal;
            return get(config);
        },
    });
}

 /**
 * @link /
 */
export function useGet<TData = GetQueryResponse, TQueryData = GetQueryResponse, TQueryKey extends QueryKey = GetQueryKey>(options: {
    query?: Partial<QueryObserverOptions<GetQueryResponse, Error, TData, TQueryData, TQueryKey>>;
    client?: Partial<RequestConfig>;
} = {}) {
    const { query: queryOptions, client: config = {} } = options ?? {};
    const queryKey = queryOptions?.queryKey ?? getQueryKey();
    const query = useQuery({
        ...getQueryOptions(config) as unknown as QueryObserverOptions,
        queryKey,
        ...queryOptions as unknown as Omit<QueryObserverOptions, "queryKey">
    }) as UseQueryResult<TData, Error> & {
        queryKey: TQueryKey;
    };
    query.queryKey = queryKey as TQueryKey;
    return query;
}