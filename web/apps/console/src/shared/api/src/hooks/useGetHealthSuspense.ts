import client from "@kubb/plugin-client/client";
import type { RequestConfig } from "@kubb/plugin-client/client";
import type { QueryKey, UseSuspenseQueryOptions, UseSuspenseQueryResult } from "@tanstack/react-query";
import type { GetHealthQueryResponse } from "../types/GetHealth.ts";
import { queryOptions, useSuspenseQuery } from "@tanstack/react-query";

 export const getHealthSuspenseQueryKey = () => [{ url: "/health" }] as const;

 export type GetHealthSuspenseQueryKey = ReturnType<typeof getHealthSuspenseQueryKey>;

 /**
 * @link /health
 */
async function getHealth(config: Partial<RequestConfig> = {}) {
    const res = await client<GetHealthQueryResponse, Error, unknown>({ method: "GET", url: `/health`, ...config });
    return res.data;
}

 export function getHealthSuspenseQueryOptions(config: Partial<RequestConfig> = {}) {
    const queryKey = getHealthSuspenseQueryKey();
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
export function useGetHealthSuspense<TData = GetHealthQueryResponse, TQueryData = GetHealthQueryResponse, TQueryKey extends QueryKey = GetHealthSuspenseQueryKey>(options: {
    query?: Partial<UseSuspenseQueryOptions<GetHealthQueryResponse, Error, TData, TQueryKey>>;
    client?: Partial<RequestConfig>;
} = {}) {
    const { query: queryOptions, client: config = {} } = options ?? {};
    const queryKey = queryOptions?.queryKey ?? getHealthSuspenseQueryKey();
    const query = useSuspenseQuery({
        ...getHealthSuspenseQueryOptions(config) as unknown as UseSuspenseQueryOptions,
        queryKey,
        ...queryOptions as unknown as Omit<UseSuspenseQueryOptions, "queryKey">
    }) as UseSuspenseQueryResult<TData, Error> & {
        queryKey: TQueryKey;
    };
    query.queryKey = queryKey as TQueryKey;
    return query;
}