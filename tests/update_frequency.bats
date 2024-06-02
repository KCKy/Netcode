#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "update frequency test slow 1" {
    prepare_log

    run_slow_1 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_1 client freq --mean 0.2 --max-dev 0.01 --max-mean-error 0.001 --warmup 10 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test slow 2" {
    prepare_log

    run_slow_2 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_2 client freq --mean 0.2 --max-dev 0.01 --max-mean-error 0.001 --warmup 10 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test slow 3" {
    prepare_log

    run_slow_3 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_3 client freq --mean 0.2 --max-dev 0.01 --max-mean-error 0.001 --warmup 10 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test slow 4" {
    prepare_log

    run_slow_4 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_4 client freq --mean 0.2 --max-dev 0.01 --max-mean-error 0.001 --warmup 10 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test fast 1" {
    prepare_log

    run_fast_1 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_1 client freq --mean 0.05 --max-dev 0.01 --max-mean-error 0.001 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}


@test "update frequency test fast 2" {
    prepare_log

    run_fast_2 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_2 client freq --mean 0.05 --max-dev 0.01 --max-mean-error 0.001 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test fast 3" {
    prepare_log

    run_fast_3 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_3 client freq --mean 0.05 --max-dev 0.01 --max-mean-error 0.001 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test fast 4" {
    prepare_log

    run_fast_4 server freq --server --duration 45 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_4 client freq --mean 0.05 --max-dev 0.01 --max-mean-error 0.001 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}
