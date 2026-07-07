import client from "@kubb/plugin-client/client";
import type { RequestConfig } from "@kubb/plugin-client/client";
import type { QueryKey, QueryObserverOptions, UseQueryResult } from "@tanstack/react-query";
import type { GetHealthQueryResponse } from "../types/GetHealth.ts";
import { queryOptions, useQuery } from "@tanstack/react-query";

 export const getHealthQueryKey = () => [{ url: "/health" }] as const;

 export type GetHealthQueryKey = ReturnType<typeof getHealthQueryKey>;

 /**
 * @link /health
 */
async function getHealth(config: Partial<RequestConfig> = {}) {
    const res = await client<GetHealthQueryResponse, Error, unknown>({ method: "GET", url: `/health`, ...config });
    return res.data;
}

 export function getHealthQueryOptions(config: Partial<RequestConfig> = {}) {
    const queryKey = getHealthQueryKey();
    return queryOptions({
        queryKey,
        queryFn: async ({ signal }) => {
            config.signal = signal;
            return getHealth(config);
        },
    });
}

 /**
 * @link /health
 */
export function useGetHealth<TData = GetHealthQueryResponse, TQueryData = GetHealthQueryResponse, TQueryKey extends QueryKey = GetHealthQueryKey>(options: {
    query?: Partial<QueryObserverOptions<GetHealthQueryResponse, Error, TData, TQueryData, TQueryKey>>;
    client?: Partial<RequestConfig>;
} = {}) {
    const { query: queryOptions, client: config = {} } = options ?? {};
    const queryKey = queryOptions?.queryKey ?? getHealthQueryKey();
    const query = useQuery({
        ...getHealthQueryOptions(config) as unknown as QueryObserverOptions,
        queryKey,
        ...queryOptions as unknown as Omit<QueryObserverOptions, "queryKey">
    }) as UseQueryResult<TData, Error> & {
        queryKey: TQueryKey;
    };
    query.queryKey = queryKey as TQueryKey;
    return query;
}